using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawboardCodingExercise.Contracts;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Events;
using DrawboardCodingExercise.Contracts.Services;

namespace DrawboardCodingExercise.ViewModel;

/// <summary>
/// Backs the application shell: the title bar, the back button, and the aggregated progress indicator.
/// </summary>
/// <remarks>
/// Constructed once at startup and never navigated to, so unlike page ViewModels it lives for the lifetime of
/// the application. Progress is aggregated rather than boolean: several operations can be in flight at once, so
/// the indicator clears only when the last of them finishes.
/// </remarks>
public partial class ShellViewModel : ObservableObject, INavigateToAware, IDisposable
{
	private readonly INavigationService _navigationService;
	private readonly IEventAggregator _eventAggregator;

	private readonly CompositeDisposable _subscriptions = new();
	private readonly ObservableCollection<string> _thingsInProgress = new();

	/// <summary>Whether any operation is currently in progress.</summary>
	[ObservableProperty] private bool _isBusy;

	/// <summary>The description of the operation currently shown to the user, if any.</summary>
	[ObservableProperty] private string? _thingInProgress;

	/// <summary>
	/// Initializes a new instance of the <see cref="ShellViewModel"/> class.
	/// </summary>
	/// <param name="navigationService">Drives the initial navigation and reports navigation changes.</param>
	/// <param name="eventAggregator">Carries the busy and done notifications this shell reflects.</param>
	public ShellViewModel(
		INavigationService navigationService,
		IEventAggregator eventAggregator)
	{
		_navigationService = navigationService;
		_eventAggregator = eventAggregator;
		_navigationService.Navigated += NavigationService_OnNavigated;
	}

	/// <summary>
	/// Gets a value indicating whether there is a page to go back to.
	/// </summary>
	public bool CanGoBack => _navigationService.CanGoBack;

	/// <summary>
	/// Returns to the previous page.
	/// </summary>
	/// <returns>A task that completes when the previous page has finished reloading.</returns>
	[RelayCommand(CanExecute = nameof(CanGoBack))]
	private async Task OnGoBack()
	{
		await _navigationService.BackAsync();
	}

	/// <summary>
	/// Re-evaluates the back button whenever the frame's content changes, in either direction.
	/// </summary>
	private void NavigationService_OnNavigated()
	{
		GoBackCommand.NotifyCanExecuteChanged();
		OnPropertyChanged(nameof(CanGoBack));
	}

	/// <inheritdoc />
	/// <remarks>
	/// Invoked directly by the application at startup rather than by the navigation service, because the shell
	/// hosts the frame instead of living inside it. Subscribes to progress notifications before performing the
	/// initial navigation, so the first page's own loading is reflected in the indicator.
	/// </remarks>
	public async Task OnNavigatedToAsync(object? parameter)
	{
		_subscriptions.Add(_eventAggregator.SubscribeOnUI<NotifyBusyEvent>(OnNotifyBusy));
		_subscriptions.Add(_eventAggregator.SubscribeOnUI<NotifyDoneEvent>(OnNotifyDone));

		await _navigationService.NavigateAsync(PageKey.FilmList).ConfigureAwait(true);
	}

	/// <summary>
	/// Clears a completed operation from the progress indicator.
	/// </summary>
	/// <param name="obj">The completion notification, matched by its text against the busy notification.</param>
	/// <remarks>
	/// Tolerates a notification that matches nothing. Operations are meant to be paired by
	/// <see cref="IBusyOperationRunner"/>, but an unmatched completion must not take the application down —
	/// removing at an unfound index would throw on the UI thread, from an event handler, which is unrecoverable.
	/// </remarks>
	private void OnNotifyDone(NotifyDoneEvent obj)
	{
		var index = _thingsInProgress.IndexOf(obj.Event);

		if (index >= 0)
		{
			_thingsInProgress.RemoveAt(index);
		}

		IsBusy = _thingsInProgress.Count > 0;
		ThingInProgress = _thingsInProgress.FirstOrDefault();
	}

	/// <summary>
	/// Adds a started operation to the progress indicator.
	/// </summary>
	/// <param name="obj">The busy notification, whose text is displayed and used as the correlation key.</param>
	private void OnNotifyBusy(NotifyBusyEvent obj)
	{
		_thingsInProgress.Add(obj.Event);
		IsBusy = true;
		ThingInProgress = _thingsInProgress.FirstOrDefault();
	}

	/// <summary>
	/// Releases the progress subscriptions and detaches from the navigation service.
	/// </summary>
	public void Dispose()
	{
		_navigationService.Navigated -= NavigationService_OnNavigated;
		_subscriptions.Dispose();
	}
}
