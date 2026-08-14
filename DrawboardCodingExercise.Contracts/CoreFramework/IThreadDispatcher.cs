using System;
using System.Threading.Tasks;

namespace DrawboardCodingExercise.Contracts.CoreFramework;

/// <summary>
/// Marshals work onto the UI thread. Abstracted so that portable ViewModels and services can update bound
/// state without referencing a platform dispatcher, and so tests can run callbacks inline.
/// </summary>
public interface IThreadDispatcher
{
	/// <summary>
	/// Runs <paramref name="func"/> on the UI thread and awaits it.
	/// </summary>
	/// <param name="func">The work to run. Invoked inline when the caller is already on the UI thread.</param>
	/// <returns>A task that completes when the work has finished.</returns>
	Task RunOnUIThreadAsync(Func<Task> func);

	/// <summary>
	/// Gets a value indicating whether the calling thread is the UI thread.
	/// </summary>
	/// <value><see langword="true"/> when the caller may touch UI state directly.</value>
	bool OnUIThread { get; }

	/// <summary>
	/// Schedules <paramref name="action"/> on the UI thread without waiting for it.
	/// </summary>
	/// <param name="action">
	/// The work to run. Invoked inline when the caller is already on the UI thread; otherwise queued, so it
	/// may not have run by the time this method returns. Exceptions are logged and swallowed rather than
	/// escaping to an unobserved task.
	/// </param>
	void FireOnUIAndForget(Action action);
}
