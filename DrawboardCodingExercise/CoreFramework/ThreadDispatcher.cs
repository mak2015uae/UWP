using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using DrawboardCodingExercise.Contracts.CoreFramework;
using Serilog;

namespace DrawboardCodingExercise.CoreFramework;

/// <summary>
///     Delegates the running of a set of code to happening on the UI thread.
/// </summary>
public class ThreadDispatcher : IThreadDispatcher
{
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ThreadDispatcher"/> class.
	/// </summary>
	/// <param name="logger">Receives failures from fire-and-forget dispatches, which have nowhere else to go.</param>
	public ThreadDispatcher(ILogger logger)
	{
		_logger = logger;
	}

	/// <summary>
	///     Forces a block of code to run on the UI thread. If the code is already running on the UI thread, it is invoked
	///     immediately.
	/// </summary>
	/// <param name="function">The code to run</param>
	/// <returns>
	/// A task that completes when <paramref name="function"/>'s own task completes — not merely when it has been
	/// started — and that carries any exception it threw.
	/// </returns>
	/// <remarks>
	/// The platform dispatch primitive accepts a void-returning handler, so handing it an asynchronous callback
	/// directly would discard the inner task and let the caller resume as soon as the callback yielded. A
	/// completion source bridges that gap, which matters here because callers await this to know a page has
	/// finished loading.
	/// </remarks>
	public Task RunOnUIThreadAsync(Func<Task> function)
	{
		var coreApplicationView = CoreApplication.MainView;
		var dispatcher = coreApplicationView.Dispatcher;

		if (dispatcher.HasThreadAccess)
		{
			return function();
		}

		var completion = new TaskCompletionSource<object>();

		_ = dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
		{
			try
			{
				await function();
				completion.TrySetResult(null);
			}
			catch (Exception e)
			{
				completion.TrySetException(e);
			}
		});

		return completion.Task;
	}

	/// <summary>
	///     Forces a block of code to run on the UI thread. If the code is already running on the UI thread, it is invoked
	///     immediately.
	/// </summary>
	/// <param name="function">The code to run</param>
	/// <remarks>
	/// Returns before <paramref name="function"/> has necessarily run when called from a worker thread, and
	/// failures are logged rather than surfaced, so use this only where nothing depends on completion.
	/// </remarks>
	public void FireOnUIAndForget(Action function)
	{
		var coreApplicationView = CoreApplication.MainView;
		var dispatcher = coreApplicationView.Dispatcher;

		if (dispatcher.HasThreadAccess)
		{
			function();
		}
		else
		{
			_ = Task.Run(async () =>
			{
				try
				{
					await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => function());
				}
				catch (Exception e)
				{
					_logger.Error(e, "Error in a FireOnUIAndForget operation");
				}
			});
		}
	}

	/// <inheritdoc />
	/// <exception cref="Exception">
	/// The window's dispatcher is not yet available, which means the caller ran before the application window
	/// was created.
	/// </exception>
	public bool OnUIThread => CoreApplication.MainView?.Dispatcher?.HasThreadAccess ??
	                          throw new Exception(
		                          "Attempt to dispatch to the UI thread before the dispatcher was available");
}
