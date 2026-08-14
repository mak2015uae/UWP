using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Events;
using DrawboardCodingExercise.Contracts.Services;
using Serilog;

namespace DrawboardCodingExercise.Services;

/// <summary>
/// The default <see cref="IBusyOperationRunner"/>: reports progress to the shell, logs failures, and offers
/// the user a retry before giving up.
/// </summary>
/// <remarks>
/// This is the only place in the application that posts busy and done notifications, which is what guarantees
/// they always pair — the shell matches them by message text, so a missing done notification would leave the
/// progress indicator running forever.
/// </remarks>
public sealed class BusyOperationRunner : IBusyOperationRunner
{
	private readonly IEventAggregator _eventAggregator;
	private readonly IUserInteractionService _userInteractionService;
	private readonly IThreadDispatcher _threadDispatcher;
	private readonly ILocalizationService _localizationService;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="BusyOperationRunner"/> class.
	/// </summary>
	/// <param name="eventAggregator">Carries the busy and done notifications to the shell.</param>
	/// <param name="userInteractionService">Presents the retry prompt.</param>
	/// <param name="threadDispatcher">Marshals the retry prompt onto the UI thread when required.</param>
	/// <param name="localizationService">Resolves the failure message shown in the prompt.</param>
	/// <param name="logger">Receives the failure diagnostics, including the user's retry decision.</param>
	public BusyOperationRunner(
		IEventAggregator eventAggregator,
		IUserInteractionService userInteractionService,
		IThreadDispatcher threadDispatcher,
		ILocalizationService localizationService,
		ILogger logger)
	{
		_eventAggregator = eventAggregator;
		_userInteractionService = userInteractionService;
		_threadDispatcher = threadDispatcher;
		_localizationService = localizationService;
		_logger = logger;
	}

	/// <inheritdoc />
	/// <remarks>
	/// Exceptions the runner does not recognise as network failures are deliberately allowed to propagate —
	/// swallowing them would hide defects behind a retry dialog. The completion notification is posted in a
	/// <c>finally</c> block, so it is sent even then.
	/// </remarks>
	public async Task<OperationOutcome<T>> RunAsync<T>(
		string busyMessage,
		Func<CancellationToken, Task<T>> operation,
		CancellationToken cancellationToken = default)
	{
		if (operation is null)
		{
			throw new ArgumentNullException(nameof(operation));
		}

		_eventAggregator.Post(new NotifyBusyEvent(busyMessage));
		try
		{
			while (true)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return OperationOutcome<T>.Failure(OperationFailureReason.Cancelled);
				}

				try
				{
					var value = await operation(cancellationToken).ConfigureAwait(true);
					return OperationOutcome<T>.Success(value);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					// The caller navigated away; nobody is waiting for the result, so this is not an error.
					_logger.Debug("Operation {BusyMessage} was cancelled", busyMessage);
					return OperationOutcome<T>.Failure(OperationFailureReason.Cancelled);
				}
				catch (Exception exception) when (IsRecoverable(exception))
				{
					_logger.Warning(exception, "Operation {BusyMessage} failed and may be retried", busyMessage);

					var choice = await AskWhetherToRetryAsync(exception).ConfigureAwait(true);

					if (choice == RetryDialogResult.Cancel)
					{
						_logger.Information("The user declined to retry {BusyMessage}", busyMessage);
						return OperationOutcome<T>.Failure(OperationFailureReason.DeclinedByUser, exception);
					}
				}
			}
		}
		finally
		{
			_eventAggregator.Post(new NotifyDoneEvent(busyMessage));
		}
	}

	/// <summary>
	/// Determines whether an exception represents a transient failure worth offering a retry for.
	/// </summary>
	/// <param name="exception">The exception the operation threw.</param>
	/// <returns>
	/// <see langword="true"/> for HTTP status failures, transport failures and request timeouts;
	/// <see langword="false"/> for anything else, which then propagates to the caller.
	/// </returns>
	/// <remarks>
	/// A cancellation whose token was not signalled is a client-side timeout rather than a deliberate
	/// abandonment, so it is treated as retryable. The genuinely-cancelled case is filtered out before this
	/// method is reached.
	/// </remarks>
	private static bool IsRecoverable(Exception exception) =>
		exception is HttpStatusException ||
		exception is HttpRequestException ||
		exception is OperationCanceledException;

	/// <summary>
	/// Prompts the user to retry, on the UI thread.
	/// </summary>
	/// <param name="exception">The failure being reported, used to select the message.</param>
	/// <returns>The user's choice.</returns>
	/// <remarks>
	/// Deliberately does not use <see cref="IThreadDispatcher.RunOnUIThreadAsync"/> when off the UI thread:
	/// that overload hands an asynchronous callback to a void-returning platform dispatch primitive, so
	/// awaiting it would resume as soon as the dialog was shown rather than when the user answered. A
	/// completion source bridges the gap correctly instead.
	/// </remarks>
	private async Task<RetryDialogResult> AskWhetherToRetryAsync(Exception exception)
	{
		var message = DescribeFailure(exception);

		if (_threadDispatcher.OnUIThread)
		{
			return await _userInteractionService.ShowRetryDialogAsync(message).ConfigureAwait(true);
		}

		var choice = new TaskCompletionSource<RetryDialogResult>();

		_threadDispatcher.FireOnUIAndForget(async () =>
		{
			try
			{
				choice.TrySetResult(await _userInteractionService.ShowRetryDialogAsync(message).ConfigureAwait(true));
			}
			catch (Exception dialogFailure)
			{
				choice.TrySetException(dialogFailure);
			}
		});

		return await choice.Task.ConfigureAwait(true);
	}

	/// <summary>
	/// Selects the localized message that best explains a failure to the user.
	/// </summary>
	/// <param name="exception">The failure to describe.</param>
	/// <returns>
	/// Localized text distinguishing being offline, a missing resource, throttling and a server fault, so the
	/// user can tell whether retrying is worth their time.
	/// </returns>
	private string DescribeFailure(Exception exception)
	{
		switch (exception)
		{
			case HttpStatusException status when status.StatusCode == HttpStatusCode.NotFound:
				return _localizationService.Translate("Errors.NotFound");

			// 429 has no HttpStatusCode member in this framework version, so compare numerically.
			case HttpStatusException status when (int)status.StatusCode == 429:
				return _localizationService.Translate("Errors.RateLimited");

			case HttpStatusException status when (int)status.StatusCode >= 500:
				return _localizationService.Translate("Errors.Server");

			case HttpRequestException _:
				return _localizationService.Translate("Errors.Offline");

			case OperationCanceledException _:
				return _localizationService.Translate("Errors.Timeout");

			default:
				return _localizationService.Translate("Errors.Retry");
		}
	}
}
