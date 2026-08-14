using System;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.CoreFramework;

namespace DrawboardCodingExercise.TestSupport;

/// <summary>
/// An <see cref="IThreadDispatcher"/> that runs every callback inline on the calling thread.
/// </summary>
/// <remarks>
/// Hand-written rather than substituted because dispatch is a control-flow concern: a substitute that dropped
/// callbacks would make incremental-update tests pass while asserting nothing, and one that queued them would
/// make them intermittently fail. Running inline makes them deterministic.
/// </remarks>
public sealed class ImmediateThreadDispatcher : IThreadDispatcher
{
	/// <inheritdoc />
	/// <remarks>Always <see langword="true"/>, so code under test takes its already-on-the-UI-thread path.</remarks>
	public bool OnUIThread => true;

	/// <inheritdoc />
	public Task RunOnUIThreadAsync(Func<Task> func) => func();

	/// <inheritdoc />
	/// <remarks>
	/// Runs synchronously, so unlike the production dispatcher the work has definitely completed by the time
	/// this returns. Exceptions propagate to the caller instead of being logged and swallowed.
	/// </remarks>
	public void FireOnUIAndForget(Action action) => action();
}
