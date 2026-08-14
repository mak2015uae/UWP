using System;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DrawboardCodingExercise.Contracts.CoreFramework;

namespace DrawboardCodingExercise.ViewModel.Infrastructure;

/// <summary>
/// Base class for page ViewModels that load data, giving each page a cancellation token that is signalled once
/// the user navigates away.
/// </summary>
/// <remarks>
/// The navigation framework offers a navigated-to hook but no navigated-from hook, so a page cannot otherwise
/// tell that its work has become irrelevant. This class infers it from the navigation service's
/// <see cref="INavigationService.Navigated"/> event: a page subscribes from inside its own navigated-to hook,
/// by which point its own navigation event has already been raised, so the next event it observes must be a
/// navigation away.
/// <para>
/// The subscription removes itself the first time it fires. That matters because page ViewModels are created
/// per navigation and never disposed by the framework, so a subscription to the single-instance navigation
/// service would otherwise accumulate one dead handler per page visit.
/// </para>
/// </remarks>
public abstract class PageViewModelBase : ObservableObject, IDisposable
{
	private readonly INavigationService _navigationService;
	private CancellationTokenSource? _pageLifetime;
	private bool _isSubscribed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PageViewModelBase"/> class.
	/// </summary>
	/// <param name="navigationService">
	/// The navigation service whose navigation events mark the end of this page's lifetime.
	/// </param>
	protected PageViewModelBase(INavigationService navigationService)
	{
		_navigationService = navigationService;
	}

	/// <summary>
	/// Gets the token that is cancelled when the user navigates away from this page.
	/// </summary>
	/// <value>
	/// A live token after <see cref="BeginPageLifetime"/> has been called; otherwise
	/// <see cref="CancellationToken.None"/>. Pass it to every asynchronous load so that abandoned work stops
	/// promptly instead of completing into a page nobody is looking at.
	/// </value>
	protected CancellationToken PageLifetimeToken => _pageLifetime?.Token ?? CancellationToken.None;

	/// <summary>
	/// Starts this page's lifetime. Call once, at the top of the navigated-to hook, before loading anything.
	/// </summary>
	/// <remarks>Calling it more than once is harmless; subsequent calls are ignored.</remarks>
	protected void BeginPageLifetime()
	{
		if (_isSubscribed)
		{
			return;
		}

		_pageLifetime = new CancellationTokenSource();
		_navigationService.Navigated += OnNavigatedElsewhere;
		_isSubscribed = true;
	}

	/// <summary>
	/// Ends this page's lifetime when the frame moves to another page.
	/// </summary>
	private void OnNavigatedElsewhere()
	{
		Unsubscribe();

		try
		{
			_pageLifetime?.Cancel();
		}
		catch (ObjectDisposedException)
		{
			// Already disposed by a concurrent Dispose; the token is cancelled or irrelevant either way.
		}
	}

	/// <summary>
	/// Detaches from the navigation service, so no handler outlives the page.
	/// </summary>
	private void Unsubscribe()
	{
		if (!_isSubscribed)
		{
			return;
		}

		_navigationService.Navigated -= OnNavigatedElsewhere;
		_isSubscribed = false;
	}

	/// <summary>
	/// Cancels and releases this page's lifetime resources.
	/// </summary>
	/// <remarks>
	/// The navigation framework does not dispose page ViewModels, so this exists for tests and for any future
	/// caller that does manage their lifetime. Correctness does not depend on it being called, because the
	/// navigation subscription already removes itself.
	/// </remarks>
	public void Dispose()
	{
		Unsubscribe();

		_pageLifetime?.Cancel();
		_pageLifetime?.Dispose();
		_pageLifetime = null;
	}
}
