using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Model;
using DrawboardCodingExercise.Contracts.Navigation;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.TestSupport;
using DrawboardCodingExercise.ViewModel.Items;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.ViewModel.UnitTests;

/// <summary>
/// Tests for <see cref="FilmListViewModel"/>: what the landing page shows for each load outcome, and what it
/// does when a row is chosen.
/// </summary>
/// <remarks>
/// Runs with no UI host and no network, which is the point of keeping the ViewModels in a platform-neutral
/// project: every dependency is an interface from the contracts assembly.
/// </remarks>
public class FilmListViewModelTests
{
	private readonly IFilmService _filmService = Substitute.For<IFilmService>();
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
	private readonly FakeBusyOperationRunner _operationRunner = new();
	private readonly KeyEchoLocalizationService _localization = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="FilmListViewModelTests"/> class, giving the substituted
	/// navigation service a completed task so awaiting it does not fault.
	/// </summary>
	public FilmListViewModelTests()
	{
		_navigationService.NavigateAsync(Arg.Any<PageKey>(), Arg.Any<object>()).Returns(Task.CompletedTask);
	}

	/// <summary>
	/// Creates the ViewModel under test.
	/// </summary>
	/// <returns>A ViewModel wired to the substitutes.</returns>
	private FilmListViewModel CreateViewModel() =>
		new(_filmService, _operationRunner, _navigationService, _localization);

	/// <summary>
	/// Configures the film service to return the supplied films.
	/// </summary>
	/// <param name="films">The films to return.</param>
	private void GivenFilms(params Film[] films) =>
		_filmService.GetFilmsAsync(Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<Film>>(films));

	/// <summary>
	/// A successful load fills the list, preserving the order the service supplied.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_Success_PopulatesTheListInServiceOrder()
	{
		GivenFilms(
			TestFilms.Create(id: 1, title: "A New Hope", episodeNumber: 4),
			TestFilms.Create(id: 2, title: "The Empire Strikes Back", episodeNumber: 5));
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(null);

		viewModel.Films.Select(film => film.Title)
			.ShouldBe(new[] { "A New Hope", "The Empire Strikes Back" });
		viewModel.HasFilms.ShouldBeTrue();
		viewModel.HasError.ShouldBeFalse();
		viewModel.IsEmpty.ShouldBeFalse();
	}

	/// <summary>
	/// Each row carries both pieces of information the requirement asks for: the title and the episode, the
	/// latter formatted as a Roman numeral through the localized format string.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_Success_FormatsTheEpisodeAsALocalizedRomanNumeral()
	{
		GivenFilms(TestFilms.Create(episodeNumber: 4));
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(null);

		viewModel.Films.ShouldHaveSingleItem().EpisodeLabel.ShouldBe("Film.EpisodeFormat(IV)");
	}

	/// <summary>
	/// The busy message is localized rather than hard-coded, so the shell shows translated progress text.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_ReportsProgressUnderALocalizedMessage()
	{
		GivenFilms();
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(null);

		_operationRunner.BusyMessages.ShouldBe(new[] { "Films.Loading" });
	}

	/// <summary>
	/// A declined failure leaves a readable error state and no stale rows.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_FailureDeclinedByUser_ShowsAnErrorAndNoRows()
	{
		_operationRunner.FailuresByBusyMessage["Films.Loading"] = OperationFailureReason.DeclinedByUser;
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(null);

		viewModel.HasError.ShouldBeTrue();
		viewModel.ErrorMessage.ShouldBe("Films.LoadFailed");
		viewModel.Films.ShouldBeEmpty();
		viewModel.IsEmpty.ShouldBeFalse("a failure is not an empty result, and saying so would be misleading");
	}

	/// <summary>
	/// Cancellation means the user has already navigated away, so the page must stay silent rather than flashing
	/// an error as it disappears.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_Cancelled_ShowsNothingAtAll()
	{
		_operationRunner.FailuresByBusyMessage["Films.Loading"] = OperationFailureReason.Cancelled;
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(null);

		viewModel.HasError.ShouldBeFalse();
		viewModel.IsEmpty.ShouldBeFalse();
		viewModel.HasCompletedLoad.ShouldBeFalse();
	}

	/// <summary>
	/// An empty result is reported as empty, which is a different message from a failure.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_NoFilmsReturned_ShowsTheEmptyState()
	{
		GivenFilms();
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(null);

		viewModel.IsEmpty.ShouldBeTrue();
		viewModel.HasError.ShouldBeFalse();
	}

	/// <summary>
	/// The empty state must not appear before a load has finished, or it would flash up during startup.
	/// </summary>
	[Fact]
	public void IsEmpty_BeforeAnyLoad_IsFalse()
	{
		CreateViewModel().IsEmpty.ShouldBeFalse();
	}

	/// <summary>
	/// Choosing a row navigates to the detail page carrying the film's identifier — a value, so that back
	/// navigation can replay it.
	/// </summary>
	[Fact]
	public async Task FilmSelectedCommand_NavigatesToTheDetailPageWithTheFilmIdentifier()
	{
		var viewModel = CreateViewModel();

		await viewModel.FilmSelectedCommand.ExecuteAsync(new FilmListItemViewModel(7, "A New Hope", "Episode IV"));

		await _navigationService.Received(1).NavigateAsync(
			PageKey.FilmDetail,
			Arg.Is<FilmDetailParameter>(parameter => parameter.FilmId == 7));
	}

	/// <summary>
	/// A click that resolves to no row must be ignored rather than navigating to a nonexistent film.
	/// </summary>
	[Fact]
	public async Task FilmSelectedCommand_NoRow_DoesNotNavigate()
	{
		var viewModel = CreateViewModel();

		await viewModel.FilmSelectedCommand.ExecuteAsync(null);

		await _navigationService.DidNotReceive().NavigateAsync(Arg.Any<PageKey>(), Arg.Any<object>());
	}

	/// <summary>
	/// The page's load token is cancelled once the frame moves elsewhere, so work for an abandoned page stops.
	/// The subscription is also removed, which is what stops one dead handler accumulating per page visit.
	/// </summary>
	[Fact]
	public async Task NavigatingAway_CancelsThePagesToken_AndDetachesFromTheNavigationService()
	{
		CancellationToken observedToken = default;
		_filmService.GetFilmsAsync(Arg.Any<CancellationToken>()).Returns(call =>
		{
			observedToken = call.Arg<CancellationToken>();
			return Task.FromResult<IReadOnlyList<Film>>(Array.Empty<Film>());
		});
		var viewModel = CreateViewModel();
		await viewModel.OnNavigatedToAsync(null);

		observedToken.IsCancellationRequested.ShouldBeFalse();

		_navigationService.Navigated += Raise.Event<Action>();

		observedToken.IsCancellationRequested.ShouldBeTrue("navigating away abandons the page's work");

		// A second navigation must find no handler still attached.
		_navigationService.Navigated += Raise.Event<Action>();
		Should.NotThrow(() => viewModel.Dispose());
	}
}
