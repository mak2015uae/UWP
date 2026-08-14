using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Events;
using DrawboardCodingExercise.TestSupport;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.ViewModel.UnitTests;

/// <summary>
/// Tests for <see cref="ShellViewModel"/>: the aggregated progress indicator and the back button.
/// </summary>
public class ShellViewModelTests
{
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
	private readonly RecordingEventAggregator _eventAggregator = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ShellViewModelTests"/> class, giving the substituted
	/// navigation service completed tasks so awaiting it does not fault.
	/// </summary>
	public ShellViewModelTests()
	{
		_navigationService.NavigateAsync(Arg.Any<PageKey>(), Arg.Any<object>()).Returns(Task.CompletedTask);
		_navigationService.BackAsync().Returns(Task.CompletedTask);
	}

	/// <summary>
	/// Creates a shell that has completed its startup, so it is subscribed to progress notifications.
	/// </summary>
	/// <returns>The started shell ViewModel.</returns>
	private async Task<ShellViewModel> CreateStartedShellAsync()
	{
		var viewModel = new ShellViewModel(_navigationService, _eventAggregator);
		await viewModel.OnNavigatedToAsync(null);
		return viewModel;
	}

	/// <summary>
	/// Startup opens the film list, which is the application's landing page.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_NavigatesToTheFilmList()
	{
		await CreateStartedShellAsync();

		await _navigationService.Received(1).NavigateAsync(PageKey.FilmList, Arg.Any<object>());
	}

	/// <summary>
	/// A busy notification shows the indicator along with the description the user should see.
	/// </summary>
	[Fact]
	public async Task NotifyBusy_ShowsTheProgressIndicator()
	{
		var viewModel = await CreateStartedShellAsync();

		_eventAggregator.Post(new NotifyBusyEvent("Loading films"));

		viewModel.IsBusy.ShouldBeTrue();
		viewModel.ThingInProgress.ShouldBe("Loading films");
	}

	/// <summary>
	/// The matching completion notification clears it again.
	/// </summary>
	[Fact]
	public async Task NotifyDone_MatchingBusy_ClearsTheProgressIndicator()
	{
		var viewModel = await CreateStartedShellAsync();

		_eventAggregator.Post(new NotifyBusyEvent("Loading films"));
		_eventAggregator.Post(new NotifyDoneEvent("Loading films"));

		viewModel.IsBusy.ShouldBeFalse();
		viewModel.ThingInProgress.ShouldBeNull();
	}

	/// <summary>
	/// Progress is aggregated, so the indicator stays up until the last operation finishes. The detail page runs
	/// two loads at once, so this is the ordinary case rather than an edge one.
	/// </summary>
	[Fact]
	public async Task NotifyDone_WithAnotherOperationStillRunning_KeepsTheIndicatorUp()
	{
		var viewModel = await CreateStartedShellAsync();

		_eventAggregator.Post(new NotifyBusyEvent("Loading film"));
		_eventAggregator.Post(new NotifyBusyEvent("Loading characters"));
		_eventAggregator.Post(new NotifyDoneEvent("Loading film"));

		viewModel.IsBusy.ShouldBeTrue();
		viewModel.ThingInProgress.ShouldBe("Loading characters");

		_eventAggregator.Post(new NotifyDoneEvent("Loading characters"));

		viewModel.IsBusy.ShouldBeFalse();
	}

	/// <summary>
	/// A completion notification matching nothing must not take the application down.
	/// </summary>
	/// <remarks>
	/// Regression test. Removing at the index of an absent entry throws, and this handler runs on the UI thread
	/// from an event callback, where an exception is unrecoverable. Pairing is the runner's job, but the shell
	/// must not be the thing that crashes when a caller gets it wrong.
	/// </remarks>
	[Fact]
	public async Task NotifyDone_WithNoMatchingBusy_IsIgnoredRatherThanThrowing()
	{
		var viewModel = await CreateStartedShellAsync();

		Should.NotThrow(() => _eventAggregator.Post(new NotifyDoneEvent("never started")));

		viewModel.IsBusy.ShouldBeFalse();
	}

	/// <summary>
	/// An unmatched completion must not disturb an operation that is genuinely running.
	/// </summary>
	[Fact]
	public async Task NotifyDone_WithNoMatchingBusy_LeavesRunningOperationsAlone()
	{
		var viewModel = await CreateStartedShellAsync();

		_eventAggregator.Post(new NotifyBusyEvent("Loading films"));
		_eventAggregator.Post(new NotifyDoneEvent("something else"));

		viewModel.IsBusy.ShouldBeTrue();
		viewModel.ThingInProgress.ShouldBe("Loading films");
	}

	/// <summary>
	/// The back button re-evaluates whenever the frame's content changes. This is why the navigation service must
	/// raise its event when going back as well as forward.
	/// </summary>
	[Fact]
	public void Navigated_RaisesChangeNotificationForCanGoBack()
	{
		var viewModel = new ShellViewModel(_navigationService, _eventAggregator);
		var changedProperties = new List<string?>();
		((INotifyPropertyChanged)viewModel).PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

		_navigationService.Navigated += Raise.Event<Action>();

		changedProperties.ShouldContain(nameof(ShellViewModel.CanGoBack));
	}

	/// <summary>
	/// The back command reports availability from the navigation stack, so the button disables at the root.
	/// </summary>
	/// <param name="canGoBack">Whether the navigation service reports a back entry.</param>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void GoBackCommand_FollowsTheNavigationStack(bool canGoBack)
	{
		_navigationService.CanGoBack.Returns(canGoBack);
		var viewModel = new ShellViewModel(_navigationService, _eventAggregator);

		viewModel.CanGoBack.ShouldBe(canGoBack);
		viewModel.GoBackCommand.CanExecute(null).ShouldBe(canGoBack);
	}

	/// <summary>
	/// Executing the command asks the navigation service to go back.
	/// </summary>
	[Fact]
	public async Task GoBackCommand_AsksTheNavigationServiceToGoBack()
	{
		_navigationService.CanGoBack.Returns(true);
		var viewModel = new ShellViewModel(_navigationService, _eventAggregator);

		await viewModel.GoBackCommand.ExecuteAsync(null);

		await _navigationService.Received(1).BackAsync();
	}

	/// <summary>
	/// Disposing releases the progress subscriptions, so a disposed shell stops reacting.
	/// </summary>
	[Fact]
	public async Task Dispose_StopsReactingToProgressNotifications()
	{
		var viewModel = await CreateStartedShellAsync();

		viewModel.Dispose();
		_eventAggregator.Post(new NotifyBusyEvent("Loading films"));

		viewModel.IsBusy.ShouldBeFalse();
	}
}
