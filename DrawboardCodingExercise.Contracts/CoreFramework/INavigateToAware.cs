using System.Threading.Tasks;

namespace DrawboardCodingExercise.Contracts.CoreFramework;

/// <summary>
/// Notifies when the ViewModel has been constructed and attached to a view and is ready to retrieve related data, etc.
/// </summary>
public interface INavigateToAware
{
	/// <summary>
	/// Notify that a navigation has completed
	/// </summary>
	/// <param name="parameter">
	/// Any additional data about the navigation; <see langword="null"/> when the caller passed none.
	/// Implementations should tolerate an unexpected type rather than casting blindly.
	/// </param>
	/// <returns>
	/// A task that completes when the page has finished loading. The navigation service awaits it, so a
	/// long-running load delays the completion of the navigation itself — report progress rather than
	/// blocking silently.
	/// </returns>
	/// <remarks>
	/// Invoked on the UI thread after the view's data context has been assigned. Exceptions escaping this
	/// method are logged as fatal by the navigation service and rethrown.
	/// </remarks>
	Task OnNavigatedToAsync(object? parameter);
}
