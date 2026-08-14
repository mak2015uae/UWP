using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.Events;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.TestSupport;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests;

/// <summary>
/// Tests for <see cref="BusyOperationRunner"/>: the busy/done pairing the shell depends on, the retry loop, and
/// the mapping from failure to explanation.
/// </summary>
/// <remarks>
/// The pairing tests matter more than they look: the shell clears its progress entry by matching message text,
/// so an unsent or mismatched completion notification leaves the progress indicator spinning for the rest of the
/// session. Confining that logic to this one class is only safe if it is verified here.
/// </remarks>
public class BusyOperationRunnerTests
{
	private const string BusyMessage = "Loading films";

	private readonly RecordingEventAggregator _eventAggregator = new();
	private readonly IUserInteractionService _userInteraction = Substitute.For<IUserInteractionService>();
	private readonly ImmediateThreadDispatcher _threadDispatcher = new();
	private readonly KeyEchoLocalizationService _localization = new();

	/// <summary>
	/// Creates the runner under test.
	/// </summary>
	/// <returns>A runner wired to the recording aggregator and substituted prompt.</returns>
	private BusyOperationRunner CreateRunner() =>
		new(_eventAggregator, _userInteraction, _threadDispatcher, _localization, SilentLogger.Create());

	/// <summary>
	/// Counts the busy notifications posted.
	/// </summary>
	private int BusyCount => _eventAggregator.PostedMessages.OfType<NotifyBusyEvent>().Count();

	/// <summary>
	/// Counts the completion notifications posted.
	/// </summary>
	private int DoneCount => _eventAggregator.PostedMessages.OfType<NotifyDoneEvent>().Count();

	/// <summary>
	/// The happy path: one busy notification, one completion notification, and identical text so the shell can
	/// match them.
	/// </summary>
	[Fact]
	public async Task RunAsync_Success_PostsExactlyOnePairedBusyAndDone()
	{
		var outcome = await CreateRunner().RunAsync(BusyMessage, _ => Task.FromResult(42));

		outcome.IsSuccessful.ShouldBeTrue();
		outcome.Value.ShouldBe(42);
		BusyCount.ShouldBe(1);
		DoneCount.ShouldBe(1);
		_eventAggregator.PostedMessages.OfType<NotifyBusyEvent>().Single().Event
			.ShouldBe(_eventAggregator.PostedMessages.OfType<NotifyDoneEvent>().Single().Event);
	}

	/// <summary>
	/// Retrying runs the operation again but must still leave the shell balanced — one completion, not one per
	/// attempt.
	/// </summary>
	[Fact]
	public async Task RunAsync_FailureThenRetry_RunsAgainAndStillPostsOneDone()
	{
		_userInteraction.ShowRetryDialogAsync(Arg.Any<string>()).Returns(
			Task.FromResult(RetryDialogResult.Retry),
			Task.FromResult(RetryDialogResult.Cancel));

		var attempts = 0;

		var outcome = await CreateRunner().RunAsync<int>(BusyMessage, _ =>
		{
			attempts++;
			return attempts == 1
				? throw new HttpStatusException(HttpStatusCode.InternalServerError)
				: Task.FromResult(7);
		});

		attempts.ShouldBe(2);
		outcome.IsSuccessful.ShouldBeTrue();
		outcome.Value.ShouldBe(7);
		BusyCount.ShouldBe(1);
		DoneCount.ShouldBe(1);
	}

	/// <summary>
	/// Declining the retry ends the operation with a reason the page can render quietly — the user has already
	/// been told, so a second prompt would be nagging.
	/// </summary>
	[Fact]
	public async Task RunAsync_FailureThenCancel_ReturnsDeclinedByUserAndCarriesTheException()
	{
		_userInteraction.ShowRetryDialogAsync(Arg.Any<string>()).Returns(RetryDialogResult.Cancel);
		var failure = new HttpStatusException(HttpStatusCode.BadGateway);

		var outcome = await CreateRunner().RunAsync<int>(BusyMessage, _ => throw failure);

		outcome.IsSuccessful.ShouldBeFalse();
		outcome.FailureReason.ShouldBe(OperationFailureReason.DeclinedByUser);
		outcome.Exception.ShouldBeSameAs(failure);
		DoneCount.ShouldBe(1);
	}

	/// <summary>
	/// An exception the runner does not recognise is a defect, so it propagates rather than hiding behind a retry
	/// prompt — but the shell is still left in a clean state.
	/// </summary>
	[Fact]
	public async Task RunAsync_UnrecognisedException_PropagatesButStillPostsDone()
	{
		await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateRunner().RunAsync<int>(BusyMessage, _ => throw new InvalidOperationException("defect")));

		DoneCount.ShouldBe(1);
		await _userInteraction.DidNotReceive().ShowRetryDialogAsync(Arg.Any<string>());
	}

	/// <summary>
	/// Cancellation is not a failure to report: the user has navigated away, so no dialog appears.
	/// </summary>
	[Fact]
	public async Task RunAsync_TokenAlreadyCancelled_ReturnsCancelledWithoutPrompting()
	{
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		var outcome = await CreateRunner().RunAsync(
			BusyMessage,
			_ => Task.FromResult(1),
			cancellation.Token);

		outcome.FailureReason.ShouldBe(OperationFailureReason.Cancelled);
		await _userInteraction.DidNotReceive().ShowRetryDialogAsync(Arg.Any<string>());
		DoneCount.ShouldBe(1);
	}

	/// <summary>
	/// An operation cancelled mid-flight by the page's own token is also silent.
	/// </summary>
	[Fact]
	public async Task RunAsync_OperationCancelledMidFlight_ReturnsCancelled()
	{
		using var cancellation = new CancellationTokenSource();

		var outcome = await CreateRunner().RunAsync<int>(
			BusyMessage,
			token =>
			{
				cancellation.Cancel();
				token.ThrowIfCancellationRequested();
				return Task.FromResult(1);
			},
			cancellation.Token);

		outcome.FailureReason.ShouldBe(OperationFailureReason.Cancelled);
		await _userInteraction.DidNotReceive().ShowRetryDialogAsync(Arg.Any<string>());
	}

	/// <summary>
	/// The explanation distinguishes the failures a user can act on differently: being offline is worth checking
	/// a connection over, a server fault is worth waiting out, and throttling is worth pausing for.
	/// </summary>
	/// <param name="statusCode">The status code to simulate, or zero to simulate a transport failure.</param>
	/// <param name="expectedKey">The resource key the user should be shown.</param>
	[Theory]
	[InlineData(404, "Errors.NotFound")]
	[InlineData(429, "Errors.RateLimited")]
	[InlineData(503, "Errors.Server")]
	[InlineData(418, "Errors.Retry")]
	[InlineData(0, "Errors.Offline")]
	public async Task RunAsync_Failure_ExplainsTheSpecificCause(int statusCode, string expectedKey)
	{
		_userInteraction.ShowRetryDialogAsync(Arg.Any<string>()).Returns(RetryDialogResult.Cancel);

		Exception failure = statusCode == 0
			? new HttpRequestException("no network")
			: new HttpStatusException((HttpStatusCode)statusCode);

		await CreateRunner().RunAsync<int>(BusyMessage, _ => throw failure);

		await _userInteraction.Received(1).ShowRetryDialogAsync(expectedKey);
	}

	/// <summary>
	/// A client-side timeout arrives as a cancellation with no token signalled, which is retryable rather than a
	/// deliberate abandonment.
	/// </summary>
	[Fact]
	public async Task RunAsync_TimeoutWithNoTokenSignalled_IsTreatedAsRetryable()
	{
		_userInteraction.ShowRetryDialogAsync(Arg.Any<string>()).Returns(RetryDialogResult.Cancel);

		var outcome = await CreateRunner().RunAsync<int>(
			BusyMessage,
			_ => throw new TaskCanceledException("the request timed out"));

		outcome.FailureReason.ShouldBe(OperationFailureReason.DeclinedByUser);
		await _userInteraction.Received(1).ShowRetryDialogAsync("Errors.Timeout");
	}

	/// <summary>
	/// A missing operation is a programming error, reported before anything is posted to the shell.
	/// </summary>
	[Fact]
	public async Task RunAsync_NullOperation_ThrowsWithoutTouchingTheShell()
	{
		await Should.ThrowAsync<ArgumentNullException>(() => CreateRunner().RunAsync<int>(BusyMessage, null!));

		_eventAggregator.PostedMessages.ShouldBeEmpty();
	}
}
