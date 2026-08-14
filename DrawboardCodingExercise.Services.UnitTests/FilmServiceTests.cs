using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.Model;
using DrawboardCodingExercise.TestSupport;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests;

/// <summary>
/// Tests for <see cref="FilmService"/>: mapping, caching, and the bounded fan-out that resolves related
/// resources.
/// </summary>
/// <remarks>
/// Driven through <see cref="StubApiClient"/>, which deserializes canned payloads using the production
/// serializer settings. That makes these tests cover the data transfer objects' property naming as well as the
/// service's own behaviour — a substitute returning ready-made objects would pass even if every wire name were
/// wrong.
/// </remarks>
public class FilmServiceTests
{
	private readonly StubApiClient _apiClient = new();

	/// <summary>
	/// Creates the service under test.
	/// </summary>
	/// <returns>A service backed by the stub client.</returns>
	private FilmService CreateService() => new(_apiClient, SilentLogger.Create());

	/// <summary>
	/// Registers the two-film payload as the films response.
	/// </summary>
	private void GivenTwoFilms() =>
		_apiClient.ResponsesByPath[SampleFilmPayloads.FilmsPath] = SampleFilmPayloads.TwoFilms;

	/// <summary>
	/// Every field the two pages display must survive the snake_case wire format and reach the domain model.
	/// </summary>
	[Fact]
	public async Task GetFilmsAsync_MapsEveryFieldTheUiNeeds()
	{
		GivenTwoFilms();

		var films = await CreateService().GetFilmsAsync();

		var newHope = films.Single(film => film.EpisodeNumber == 4);
		newHope.Id.ShouldBe(1);
		newHope.Title.ShouldBe("A New Hope");
		newHope.Director.ShouldBe("George Lucas");
		newHope.Producer.ShouldBe("Gary Kurtz, Rick McCallum");
		newHope.ReleaseDate.ShouldBe(new DateTimeOffset(1977, 5, 25, 0, 0, 0, TimeSpan.Zero));
		newHope.OpeningCrawl.ShouldStartWith("It is a period of civil war.");
		newHope.RelatedResourceUrls[RelatedResourceKind.Characters].Count.ShouldBe(3);
		newHope.RelatedResourceUrls[RelatedResourceKind.Starships].ShouldHaveSingleItem();
	}

	/// <summary>
	/// Line endings are normalized, so the crawl never renders stray carriage returns.
	/// </summary>
	[Fact]
	public async Task GetFilmsAsync_NormalizesCrawlLineEndings()
	{
		GivenTwoFilms();

		var films = await CreateService().GetFilmsAsync();

		films.ShouldAllBe(film => !film.OpeningCrawl.Contains("\r"));
	}

	/// <summary>
	/// The API hard-wraps the crawl at around thirty characters, which suits a cinema screen but leaves a narrow
	/// ragged column in a resizable window. Those wraps are dropped so the view can reflow the prose.
	/// </summary>
	[Fact]
	public async Task GetFilmsAsync_DropsTheCrawlsHardWrapsSoItCanReflow()
	{
		GivenTwoFilms();

		var crawl = (await CreateService().GetFilmsAsync())
			.Single(film => film.EpisodeNumber == 4)
			.OpeningCrawl;

		crawl.ShouldStartWith("It is a period of civil war. Rebel spaceships, striking from a hidden base.");
		crawl.ShouldNotContain("war.\nRebel", Case.Sensitive);
	}

	/// <summary>
	/// Blank lines are the author's paragraph breaks rather than an artefact of the source's line length, so they
	/// survive — the crawl still reads as paragraphs.
	/// </summary>
	[Fact]
	public async Task GetFilmsAsync_KeepsTheCrawlsParagraphBreaks()
	{
		GivenTwoFilms();

		var crawl = (await CreateService().GetFilmsAsync())
			.Single(film => film.EpisodeNumber == 4)
			.OpeningCrawl;

		crawl.ShouldContain("\n\n");
		crawl.Split(new[] { "\n\n" }, StringSplitOptions.None).Length.ShouldBe(2);
		crawl.ShouldEndWith("During the battle, Rebel spies managed to steal plans.");
	}

	/// <summary>
	/// The payload lists episode 5 before episode 4, so ordering is the service's job rather than the API's.
	/// </summary>
	[Fact]
	public async Task GetFilmsAsync_OrdersByEpisodeNumber()
	{
		GivenTwoFilms();

		var films = await CreateService().GetFilmsAsync();

		films.Select(film => film.EpisodeNumber).ShouldBe(new[] { 4, 5 });
	}

	/// <summary>
	/// The cache is what makes the detail page free and back navigation instant, so a second call must not
	/// reach the network.
	/// </summary>
	[Fact]
	public async Task GetFilmsAsync_CalledTwice_RequestsOnce()
	{
		GivenTwoFilms();
		var service = CreateService();

		await service.GetFilmsAsync();
		await service.GetFilmsAsync();

		_apiClient.RequestCount(SampleFilmPayloads.FilmsPath).ShouldBe(1);
	}

	/// <summary>
	/// Two pages loading at once must still produce a single request; the cache is guarded, not merely checked.
	/// </summary>
	[Fact]
	public async Task GetFilmsAsync_ConcurrentCallers_RequestsOnce()
	{
		GivenTwoFilms();
		_apiClient.ResponseDelay = TimeSpan.FromMilliseconds(30);
		var service = CreateService();

		await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.GetFilmsAsync()));

		_apiClient.RequestCount(SampleFilmPayloads.FilmsPath).ShouldBe(1);
	}

	/// <summary>
	/// A detail page reached with a cold cache — after a resume, or a replayed back stack — must still work.
	/// </summary>
	[Fact]
	public async Task GetFilmAsync_ColdCache_PopulatesCacheAndReturnsTheFilm()
	{
		GivenTwoFilms();

		var film = await CreateService().GetFilmAsync(1);

		film.ShouldNotBeNull();
		film!.Title.ShouldBe("A New Hope");
		_apiClient.RequestCount(SampleFilmPayloads.FilmsPath).ShouldBe(1);
	}

	/// <summary>
	/// An unknown identifier is a not-found condition for the page to report, not an exception.
	/// </summary>
	[Fact]
	public async Task GetFilmAsync_UnknownIdentifier_ReturnsNull()
	{
		GivenTwoFilms();

		var film = await CreateService().GetFilmAsync(4242);

		film.ShouldBeNull();
	}

	/// <summary>
	/// A hostile payload must not cost the user the list: absent arrays, nulls, blanks and an unparsable date
	/// all resolve to safe values.
	/// </summary>
	[Fact]
	public async Task GetFilmsAsync_AwkwardPayload_SubstitutesSafeDefaults()
	{
		_apiClient.ResponsesByPath[SampleFilmPayloads.FilmsPath] = SampleFilmPayloads.AwkwardFilm;

		var film = (await CreateService().GetFilmsAsync()).ShouldHaveSingleItem();

		film.Id.ShouldBe(9, "the identifier is parsed from a URL with a trailing slash");
		film.Title.ShouldBeEmpty();
		film.Director.ShouldBeEmpty();
		film.OpeningCrawl.ShouldBeEmpty();
		film.ReleaseDate.ShouldBeNull("an unparsable date must not become a misleading default");
		film.RelatedResourceUrls[RelatedResourceKind.Characters].ShouldHaveSingleItem();
		film.RelatedResourceUrls[RelatedResourceKind.Planets].ShouldBeEmpty("absent arrays become empty, not null");
	}

	/// <summary>
	/// An empty collection is a legitimate answer and must not be confused with a failure.
	/// </summary>
	[Fact]
	public async Task GetFilmsAsync_EmptyPayload_ReturnsEmptyCollection()
	{
		_apiClient.ResponsesByPath[SampleFilmPayloads.FilmsPath] = SampleFilmPayloads.NoFilms;

		(await CreateService().GetFilmsAsync()).ShouldBeEmpty();
	}

	/// <summary>
	/// Related resources are returned in the order the API listed them, even though the requests complete out of
	/// order.
	/// </summary>
	[Fact]
	public async Task GetRelatedResourcesAsync_PreservesTheApiOrdering()
	{
		GivenTwoFilms();
		GivenCharacters();
		var service = CreateService();
		var film = await service.GetFilmAsync(1);

		var characters = await service.GetRelatedResourcesAsync(film!, RelatedResourceKind.Characters);

		characters.Select(character => character.Name).ShouldBe(new[] { "Luke Skywalker", "C-3PO", "R2-D2" });
	}

	/// <summary>
	/// Progress is reported per resource so the page can fill its list incrementally.
	/// </summary>
	[Fact]
	public async Task GetRelatedResourcesAsync_ReportsProgressForEveryResource()
	{
		GivenTwoFilms();
		GivenCharacters();
		var service = CreateService();
		var film = await service.GetFilmAsync(1);
		var reported = new List<RelatedResource>();
		var progress = new SynchronousProgress<RelatedResource>(reported.Add);

		await service.GetRelatedResourcesAsync(film!, RelatedResourceKind.Characters, progress);

		reported.Count.ShouldBe(3);
		reported.Select(resource => resource.Kind).ShouldAllBe(kind => kind == RelatedResourceKind.Characters);
	}

	/// <summary>
	/// Concurrency is bounded, so a film with many related resources cannot flood a public API.
	/// </summary>
	[Fact]
	public async Task GetRelatedResourcesAsync_NeverExceedsTheConcurrencyLimit()
	{
		const int concurrencyLimit = 6;
		var manyCharacters = Enumerable.Range(1, 20)
			.Select(index => $"https://swapi.info/api/people/{index}")
			.ToArray();

		foreach (var url in manyCharacters)
		{
			_apiClient.ResponsesByPath[url] = SampleFilmPayloads.NamedResource($"Person {url}", url);
		}

		_apiClient.ResponseDelay = TimeSpan.FromMilliseconds(20);
		var film = FilmWithCharacters(manyCharacters);

		await CreateService().GetRelatedResourcesAsync(film, RelatedResourceKind.Characters);

		_apiClient.MaxConcurrentRequests.ShouldBeLessThanOrEqualTo(concurrencyLimit);
		_apiClient.MaxConcurrentRequests.ShouldBeGreaterThan(1, "requests should still run in parallel");
	}

	/// <summary>
	/// Resolved resources are cached by URL, so revisiting a film costs nothing.
	/// </summary>
	[Fact]
	public async Task GetRelatedResourcesAsync_CalledTwice_RequestsEachResourceOnce()
	{
		GivenTwoFilms();
		GivenCharacters();
		var service = CreateService();
		var film = await service.GetFilmAsync(1);

		await service.GetRelatedResourcesAsync(film!, RelatedResourceKind.Characters);
		await service.GetRelatedResourcesAsync(film!, RelatedResourceKind.Characters);

		_apiClient.RequestCount("people/1").ShouldBe(1);
	}

	/// <summary>
	/// A category the film lists nothing for needs no requests at all.
	/// </summary>
	[Fact]
	public async Task GetRelatedResourcesAsync_EmptyCategory_ReturnsEmptyWithoutRequesting()
	{
		GivenTwoFilms();
		var service = CreateService();
		var film = await service.GetFilmAsync(2);

		var starships = await service.GetRelatedResourcesAsync(film!, RelatedResourceKind.Starships);

		starships.ShouldBeEmpty();
		_apiClient.RequestedPaths().ShouldBe(new[] { SampleFilmPayloads.FilmsPath });
	}

	/// <summary>
	/// A server failure surfaces as the client's exception, for the caller's retry policy to deal with.
	/// </summary>
	[Fact]
	public async Task GetRelatedResourcesAsync_ServerFailure_PropagatesHttpStatusException()
	{
		GivenTwoFilms();
		GivenCharacters();
		_apiClient.FailuresByPath["people/2"] = new HttpStatusException(HttpStatusCode.InternalServerError);
		var service = CreateService();
		var film = await service.GetFilmAsync(1);

		var failure = await Should.ThrowAsync<HttpStatusException>(() =>
			service.GetRelatedResourcesAsync(film!, RelatedResourceKind.Characters));

		failure.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
	}

	/// <summary>
	/// A cancelled load stops promptly instead of completing into a page nobody is looking at.
	/// </summary>
	[Fact]
	public async Task GetRelatedResourcesAsync_CancelledToken_Throws()
	{
		GivenTwoFilms();
		GivenCharacters();
		var service = CreateService();
		var film = await service.GetFilmAsync(1);
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		await Should.ThrowAsync<OperationCanceledException>(() =>
			service.GetRelatedResourcesAsync(film!, RelatedResourceKind.Characters, null, cancellation.Token));
	}

	/// <summary>
	/// A null film is a programming error, reported as such.
	/// </summary>
	[Fact]
	public async Task GetRelatedResourcesAsync_NullFilm_Throws()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			CreateService().GetRelatedResourcesAsync(null!, RelatedResourceKind.Characters));
	}

	/// <summary>
	/// Registers the three characters the first sample film references.
	/// </summary>
	private void GivenCharacters()
	{
		_apiClient.ResponsesByPath["people/1"] =
			SampleFilmPayloads.NamedResource("Luke Skywalker", "https://swapi.info/api/people/1");
		_apiClient.ResponsesByPath["people/2"] =
			SampleFilmPayloads.NamedResource("C-3PO", "https://swapi.info/api/people/2");
		_apiClient.ResponsesByPath["people/3"] =
			SampleFilmPayloads.NamedResource("R2-D2", "https://swapi.info/api/people/3");
	}

	/// <summary>
	/// Builds a film that references the supplied character URLs and nothing else.
	/// </summary>
	/// <param name="characterUrls">The absolute character URLs to reference.</param>
	/// <returns>A film usable as input to the related-resource fan-out.</returns>
	private static Film FilmWithCharacters(IReadOnlyList<string> characterUrls) =>
		new(
			1,
			"Test film",
			1,
			null,
			string.Empty,
			string.Empty,
			string.Empty,
			new Dictionary<RelatedResourceKind, IReadOnlyList<string>>
			{
				[RelatedResourceKind.Characters] = characterUrls
			});

	/// <summary>
	/// An <see cref="IProgress{T}"/> that invokes its handler inline.
	/// </summary>
	/// <remarks>
	/// <see cref="Progress{T}"/> posts to a captured synchronization context, which a test host does not have,
	/// so reports would arrive on the thread pool after the assertions had already run.
	/// </remarks>
	/// <typeparam name="T">The reported value type.</typeparam>
	private sealed class SynchronousProgress<T> : IProgress<T>
	{
		private readonly object _gate = new();
		private readonly Action<T> _onReport;

		/// <summary>
		/// Initializes a new instance of the <see cref="SynchronousProgress{T}"/> class.
		/// </summary>
		/// <param name="onReport">The handler to invoke for each report.</param>
		public SynchronousProgress(Action<T> onReport) => _onReport = onReport;

		/// <inheritdoc />
		/// <remarks>Serialized, because the service reports from several concurrent requests.</remarks>
		public void Report(T value)
		{
			lock (_gate)
			{
				_onReport(value);
			}
		}
	}
}
