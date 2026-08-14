using System;
using System.Threading;
using System.Threading.Tasks;

namespace DrawboardCodingExercise.Contracts.Services;

/// <summary>
/// Runs an asynchronous operation with the cross-cutting concerns every data load in this application
/// shares: shell progress reporting, failure logging, and a retry prompt the user can decline.
/// </summary>
/// <remarks>
/// Exists so that no ViewModel hand-rolls the busy/done event pairing. The shell removes an in-progress
/// entry by matching its message text, so a mismatched or missing done notification leaves the progress
/// ring spinning forever; confining the pairing to one implementation makes that impossible by
/// construction, and makes it testable in one place.
/// </remarks>
public interface IBusyOperationRunner
{
	/// <summary>
	/// Runs <paramref name="operation"/> while the shell shows <paramref name="busyMessage"/>, prompting the
	/// user to retry if it fails with a transport or HTTP status error.
	/// </summary>
	/// <typeparam name="T">The type of value the operation produces.</typeparam>
	/// <param name="busyMessage">
	/// The already-localized text shown in the shell while the operation runs. The matching completion
	/// notification is posted automatically, including when the operation throws.
	/// </param>
	/// <param name="operation">
	/// The work to perform. Receives the cancellation token so it can abandon in-flight requests, and may be
	/// invoked more than once when the user chooses to retry, so it must be safe to repeat.
	/// </param>
	/// <param name="cancellationToken">
	/// A token that abandons the operation. When signalled, the outcome is
	/// <see cref="OperationFailureReason.Cancelled"/> and no dialog is shown.
	/// </param>
	/// <returns>
	/// A successful outcome carrying the operation's result, or a failed outcome describing why no value was
	/// produced. This method does not throw for anticipated network failures.
	/// </returns>
	Task<OperationOutcome<T>> RunAsync<T>(
		string busyMessage,
		Func<CancellationToken, Task<T>> operation,
		CancellationToken cancellationToken = default);
}
