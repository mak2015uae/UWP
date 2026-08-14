using System;
using System.Threading.Tasks;

namespace DrawboardCodingExercise.Contracts.CoreFramework;

/// <summary>
/// The navigation service enhances the navigation capability of a Frame in UWP.
///
/// It has the additional responsibility of resolving the View and ViewModel from a PageKey and ensuring
/// that navigation happens on the UI Thread.
///
/// If a ViewModel implements <see cref="INavigateToAware"/>, the <see cref="INavigateToAware.OnNavigatedToAsync"/>
/// method is invoked after the navigation has occurred.
/// </summary>
/// <remarks>
/// Page ViewModels are resolved per navigation, so every navigation — back navigation included — produces a
/// fresh ViewModel and re-runs its navigated-to hook. Nothing caches or disposes them, so page state does not
/// survive; cache in a shared service where persistence across navigations matters.
/// </remarks>
public interface INavigationService
{
	/// <summary>
	/// Causes a navigation to a page
	/// </summary>
	/// <param name="pageKey">The key that identifies the page</param>
	/// <param name="parameter">any object that represents the parameters being passed.</param>
	/// <returns>
	/// A task that completes once the page's ViewModel has finished its
	/// <see cref="INavigateToAware.OnNavigatedToAsync"/> hook, so awaiting this awaits the page's initial load.
	/// </returns>
	/// <remarks>
	/// Self-marshals to the UI thread, so callers need not check first. The parameter is stored in the frame's
	/// back stack and replayed verbatim on back navigation, so prefer immutable values.
	/// </remarks>
	Task NavigateAsync(PageKey pageKey, object? parameter = null);

	/// <summary>
	/// Requests the navigation service to go back one page
	/// </summary>
	/// <returns>
	/// A task that completes once the restored page's ViewModel has finished its navigated-to hook. Completes
	/// immediately when the back stack is empty.
	/// </returns>
	Task BackAsync();

	/// <summary>
	/// Raised after the frame's content has changed, in both directions, so that shell chrome such as the back
	/// button can re-evaluate its state.
	/// </summary>
	/// <remarks>Raised before the new page's navigated-to hook runs, and always on the UI thread.</remarks>
	event Action Navigated;

	/// <summary>
	/// Gets a value indicating whether there is a page to go back to.
	/// </summary>
	bool CanGoBack { get; }
}
