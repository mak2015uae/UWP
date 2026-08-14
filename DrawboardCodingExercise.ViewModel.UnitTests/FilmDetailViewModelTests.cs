using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Model;
using DrawboardCodingExercise.Contracts.Navigation;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.TestSupport;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.ViewModel.UnitTests;

/// <summary>
/// Tests for <see cref="FilmDetailViewModel"/>: the fields the requirement asks for, the opening crawl, the
/// incrementally-filled character list, and every way the page can fail to show one of them.
/// </summary>
public class FilmDetailViewModelTests
{
	private readonly IFilmService _filmService = Substitute.For<IFilmService>();
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
	private readonly FakeBusyOperationRunner _operationRunner = new();
	private readonly ImmediateThreadDispatcher _threadDispatcher = new();
	private readonly KeyEchoLocalizationService _localization = new();

	/// <summary>
	/// Creates the ViewModel under test.
	/// </summary>
	/// <returns>A ViewModel wired to the substitutes, with progress delivered inline.</returns>
	private FilmDetailViewModel CreateViewModel() =>
		new(_filmService, _operationRunner, _navigationService, _threadDispatcher, _localization);

	/// <summary>
	/// Configures the film service to return a film for its identifier.
	/// </summary>
	/// <param name="film">The film to return.</param>
	private void GivenFilm(Film film) =>
		_filmService.GetFilmAsync(film.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Film?>(film));

	/// <summary>
	/// Configures the related-resource call to report the supplied resources through the progress sink in the
	/// order given, then return them ordered as the API listed them.
	/// </summary>
	/// <param name="reportOrder">The order the responses arrive in.</param>
	/// <param name="apiOrder">The order the API listed them in.</param>
	private void GivenCharactersArriving(
		IReadOnlyList<RelatedResource> reportOrder,
		IReadOnlyList<RelatedResource> apiOrder) =>
		_filmService.GetRelatedResourcesAsync(
				Arg.Any<Film>(),
				Arg.Any<RelatedResourceKind>(),
				Arg.Any<IProgress<RelatedResource>?>(),
				Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				var progress = call.Arg<IProgress<RelatedResource>>();

				foreach (var resource in reportOrder)
				{
					progress?.Report(resource);
				}

				return Task.FromResult(apiOrder);
			});

	/// <summary>
	/// Builds a character resource for the given people identifier.
	/// </summary>
	/// <param name="index">The people resource number.</param>
	/// <param name="name">The character's name.</param>
	/// <returns>The resource, carrying the absolute URL the film would reference.</returns>
	private static RelatedResource Character(int index, string name) =>
		new(name, RelatedResourceKind.Characters, $"https://swapi.info/api/people/{index}");

	/// <summary>
	/// Every field the requirement lists for page two must be bound, including the bonus opening crawl.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_BindsEveryRequiredDetailField()
	{
		var film = TestFilms.Create(
			id: 1,
			title: "A New Hope",
			episodeNumber: 4,
			releaseDate: new DateTimeOffset(1977, 5, 25, 0, 0, 0, TimeSpan.Zero),
			director: "George Lucas",
			producer: "Gary Kurtz, Rick McCallum",
			openingCrawl: "It is a period of civil war.");
		GivenFilm(film);
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(new FilmDetailParameter(1));

		viewModel.HasFilm.ShouldBeTrue();
		viewModel.Title.ShouldBe("A New Hope");
		viewModel.EpisodeLabel.ShouldBe("Film.EpisodeFormat(IV)");
		viewModel.ReleaseDate.ShouldNotBeEmpty();
		viewModel.Director.ShouldBe("George Lucas");
		viewModel.Producer.ShouldBe("Gary Kurtz, Rick McCallum");
		viewModel.OpeningCrawl.ShouldBe("It is a period of civil war.");
		viewModel.HasOpeningCrawl.ShouldBeTrue();
		viewModel.HasError.ShouldBeFalse();
	}

	/// <summary>
	/// A film with no crawl hides that section rather than showing an empty heading.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_NoOpeningCrawl_HidesTheCrawlSection()
	{
		GivenFilm(TestFilms.Create(openingCrawl: "   "));
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(new FilmDetailParameter(1));

		viewModel.HasOpeningCrawl.ShouldBeFalse();
	}

	/// <summary>
	/// A missing release date shows a localized placeholder instead of a default date, which would be a lie.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_NoReleaseDate_ShowsALocalizedPlaceholder()
	{
		_filmService.GetFilmAsync(1, Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<Film?>(TestFilms.Create(id: 1) with { ReleaseDate = null }));
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(new FilmDetailParameter(1));

		viewModel.ReleaseDate.ShouldBe("FilmDetail.ReleaseDateUnknown");
	}

	/// <summary>
	/// Responses arrive out of order because the requests run concurrently, but the list must read in the order
	/// the API listed them — and must be in that order from the first row, not sorted at the end.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_CharactersArrivingOutOfOrder_AreListedInApiOrder()
	{
		var film = TestFilms.Create(characterUrls: TestFilms.CharacterUrls(3));
		GivenFilm(film);

		var luke = Character(1, "Luke Skywalker");
		var threepio = Character(2, "C-3PO");
		var artoo = Character(3, "R2-D2");

		// The third response arrives first, then the first, then the second.
		GivenCharactersArriving(
			new[] { artoo, luke, threepio },
			new[] { luke, threepio, artoo });

		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(new FilmDetailParameter(film.Id));

		viewModel.RelatedResources.Select(resource => resource.Name)
			.ShouldBe(new[] { "Luke Skywalker", "C-3PO", "R2-D2" });
	}

	/// <summary>
	/// A resource the film did not list is still shown rather than dropped, appended at the end.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_UnlistedCharacter_IsAppendedRatherThanDropped()
	{
		var film = TestFilms.Create(characterUrls: TestFilms.CharacterUrls(1));
		GivenFilm(film);

		var listed = Character(1, "Luke Skywalker");
		var unexpected = Character(99, "Unexpected");
		GivenCharactersArriving(new[] { unexpected, listed }, new[] { listed, unexpected });

		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(new FilmDetailParameter(film.Id));

		viewModel.RelatedResources.Select(resource => resource.Name)
			.ShouldBe(new[] { "Luke Skywalker", "Unexpected" });
	}

	/// <summary>
	/// The two loads are separate operations, so failing to resolve characters must not cost the user the film
	/// details already in hand.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_CharacterLoadFails_KeepsTheFilmDetailsOnScreen()
	{
		GivenFilm(TestFilms.Create(characterUrls: TestFilms.CharacterUrls(3)));
		_operationRunner.FailuresByBusyMessage["Characters.Loading"] = OperationFailureReason.DeclinedByUser;
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(new FilmDetailParameter(1));

		viewModel.HasFilm.ShouldBeTrue("the film's own fields need no further requests");
		viewModel.Title.ShouldBe("A New Hope");
		viewModel.HasError.ShouldBeFalse("the film itself loaded");
		viewModel.HasRelatedResourceError.ShouldBeTrue();
		viewModel.RelatedResourceErrorMessage.ShouldBe("Characters.LoadFailed");
	}

	/// <summary>
	/// A film listing no characters says so, and issues no request to find that out.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_FilmListsNoCharacters_ShowsTheEmptyStateWithoutRequesting()
	{
		GivenFilm(TestFilms.Create(characterUrls: Array.Empty<string>()));
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(new FilmDetailParameter(1));

		viewModel.HasNoRelatedResources.ShouldBeTrue();
		viewModel.RelatedResources.ShouldBeEmpty();
		await _filmService.DidNotReceive().GetRelatedResourcesAsync(
			Arg.Any<Film>(),
			Arg.Any<RelatedResourceKind>(),
			Arg.Any<IProgress<RelatedResource>?>(),
			Arg.Any<CancellationToken>());
	}

	/// <summary>
	/// Navigated to without a parameter — or with the wrong kind — the page reports not-found rather than
	/// throwing. A page should not crash because it was navigated to incorrectly.
	/// </summary>
	/// <param name="parameter">The malformed navigation parameter.</param>
	[Theory]
	[InlineData(null)]
	[InlineData("not a parameter")]
	[InlineData(42)]
	public async Task OnNavigatedToAsync_MalformedParameter_ShowsNotFound(object? parameter)
	{
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(parameter);

		viewModel.HasError.ShouldBeTrue();
		viewModel.ErrorMessage.ShouldBe("FilmDetail.NotFound");
		viewModel.HasFilm.ShouldBeFalse();
	}

	/// <summary>
	/// An identifier no film carries is a not-found condition, distinct from a load failure.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_UnknownFilm_ShowsNotFound()
	{
		_filmService.GetFilmAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<Film?>(null));
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(new FilmDetailParameter(4242));

		viewModel.ErrorMessage.ShouldBe("FilmDetail.NotFound");
		viewModel.HasFilm.ShouldBeFalse();
	}

	/// <summary>
	/// A failed film load is reported as a load failure, which tells the user retrying may help — unlike
	/// not-found, which will not change.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_FilmLoadFails_ShowsALoadFailure()
	{
		_operationRunner.FailuresByBusyMessage["FilmDetail.Loading"] = OperationFailureReason.DeclinedByUser;
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(new FilmDetailParameter(1));

		viewModel.ErrorMessage.ShouldBe("FilmDetail.LoadFailed");
		viewModel.HasFilm.ShouldBeFalse();
	}

	/// <summary>
	/// A cancelled load leaves the page silent, since the user is already looking elsewhere.
	/// </summary>
	[Fact]
	public async Task OnNavigatedToAsync_Cancelled_ShowsNothingAtAll()
	{
		_operationRunner.FailuresByBusyMessage["FilmDetail.Loading"] = OperationFailureReason.Cancelled;
		var viewModel = CreateViewModel();

		await viewModel.OnNavigatedToAsync(new FilmDetailParameter(1));

		viewModel.HasError.ShouldBeFalse();
		viewModel.HasFilm.ShouldBeFalse();
	}
}
