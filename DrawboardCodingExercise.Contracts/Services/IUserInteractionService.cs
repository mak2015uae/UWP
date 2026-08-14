using System.Threading.Tasks;

namespace DrawboardCodingExercise.Contracts.Services;

/// <summary>
/// Presents modal prompts to the user. Implemented in the UI layer so that portable code can ask a question
/// without referencing any platform dialog type.
/// </summary>
public interface IUserInteractionService
{
	/// <summary>
	/// Asks the user whether to retry a failed operation, using the default failure message.
	/// </summary>
	/// <returns>The user's choice.</returns>
	/// <remarks>Must be awaited on the UI thread; platform dialogs cannot be shown from a worker thread.</remarks>
	Task<RetryDialogResult> ShowRetryDialogAsync();

	/// <summary>
	/// Asks the user whether to retry a failed operation, explaining the specific failure.
	/// </summary>
	/// <param name="message">
	/// The already-localized explanation to show, so the user can distinguish being offline from a server
	/// fault. Callers localize; this method does not.
	/// </param>
	/// <returns>The user's choice.</returns>
	/// <remarks>Must be awaited on the UI thread; platform dialogs cannot be shown from a worker thread.</remarks>
	Task<RetryDialogResult> ShowRetryDialogAsync(string message);
}

/// <summary>
/// The choice a user made in a retry prompt.
/// </summary>
public enum RetryDialogResult
{
	/// <summary>Attempt the operation again.</summary>
	Retry,

	/// <summary>Abandon the operation; the caller should show a quiet error state.</summary>
	Cancel
}
