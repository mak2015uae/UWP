using System;
using System.Linq;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.Model;
using DrawboardCodingExercise.Services;
using DrawboardCodingExercise.Services.Api;
using DrawboardCodingExercise.TestSupport;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.IntegrationTests;

/// <summary>
/// Tests that run against the live API, verifying the assumptions the offline tests are built on.
/// </summary>
/// <remarks>
/// These require a network connection and a healthy third-party service, so they are excluded from a routine
/// run with <c>dotnet test --filter "Category!=Integration"</c> and included deliberately.
/// <para>
/// Their value is specific: the unit tests use canned payloads, so they would keep passing if the real service
/// changed shape. These assert the things that would invalidate the fixtures — that the films endpoint returns a
/// bare array rather than an envelope, that field names are snake_case, and that related resources are given as
/// absolute URLs on the same origin as the API.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public class SwapiEndpointTests
{
	/// <summary>
	/// Creates a film service talking to the live API.
	/// </summary>
	/// <returns>A service backed by a real HTTP client.</returns>
	private static FilmService CreateService()
	{
		var apiClient = new APIClient(
			new StubApiSettings(),
			ApiSerializerSettings.Create(),
			SilentLogger.Create());

		return new FilmService(apiClient, SilentLogger.Create());
	}

	/// <summary>
	/// The films endpoint returns the whole collection in one bare array, and every field the pages display
	/// survives deserialization — which is what lets the detail page work without a second request.
	/// </summary>
	[Fact]
	public async Task GetFilmsAsync_ReturnsEveryFilmWithTheFieldsBothPagesNeed()
	{
		var films = await CreateService().GetFilmsAsync();

		films.Count.ShouldBeGreaterThanOrEqualTo(6, "the API publishes at least the six original films");
		films.Select(film => film.EpisodeNumber).ShouldBeUnique();

		foreach (var film in films)
		{
			film.Title.ShouldNotBeNullOrWhiteSpace();
			film.EpisodeNumber.ShouldBeGreaterThan(0, "episode_id must have mapped through the snake_case name");
			film.Director.ShouldNotBeNullOrWhiteSpace();
			film.Producer.ShouldNotBeNullOrWhiteSpace();
			film.ReleaseDate.ShouldNotBeNull("release_date must have mapped and parsed");
			film.OpeningCrawl.ShouldNotBeNullOrWhiteSpace("opening_crawl feeds the bonus requirement");
			film.OpeningCrawl.Contains("\r").ShouldBeFalse("line endings are normalized for XAML");
			film.Id.ShouldBeGreaterThan(0, "the identifier is parsed from the resource URL");
		}
	}

	/// <summary>
	/// Films are ordered by episode, which is not the order the API lists them in.
	/// </summary>
	[Fact]
	public async Task GetFilmsAsync_OrdersByEpisodeNumber()
	{
		var films = await CreateService().GetFilmsAsync();

		films.Select(film => film.EpisodeNumber).ShouldBeInOrder();
	}

	/// <summary>
	/// Related resources are published as absolute URLs on the API's own origin. Both halves matter: absolute, so
	/// the client must resolve rather than concatenate them; same origin, so the client is willing to request
	/// them at all.
	/// </summary>
	[Fact]
	public async Task GetFilmsAsync_PublishesRelatedResourcesAsAbsoluteUrlsOnTheSameOrigin()
	{
		var films = await CreateService().GetFilmsAsync();
		var film = films.First();
		var baseUri = RequestUriResolver.CreateBaseUri(StubApiSettings.DefaultServerAddress);

		var characterUrls = film.RelatedResourceUrls[RelatedResourceKind.Characters];
		characterUrls.ShouldNotBeEmpty();

		foreach (var url in characterUrls)
		{
			Uri.IsWellFormedUriString(url, UriKind.Absolute).ShouldBeTrue($"'{url}' should be absolute");

			// Throws if the URL is not beneath the configured base address, and returns it unchanged rather than
			// appending it to the base — the defect this whole seam exists to prevent.
			RequestUriResolver.Resolve(baseUri, url).AbsoluteUri.ShouldBe(url);
		}
	}

	/// <summary>
	/// The related resources of a real film resolve to real display names through the bounded fan-out.
	/// </summary>
	[Fact]
	public async Task GetRelatedResourcesAsync_ResolvesRealCharacterNames()
	{
		var service = CreateService();
		var films = await service.GetFilmsAsync();
		var film = films.First();

		var characters = await service.GetRelatedResourcesAsync(film, RelatedResourceKind.Characters);

		characters.Count.ShouldBe(film.RelatedResourceUrls[RelatedResourceKind.Characters].Count);
		characters.ShouldAllBe(character => !string.IsNullOrWhiteSpace(character.Name));
		characters.ShouldAllBe(character => character.Kind == RelatedResourceKind.Characters);
	}

	/// <summary>
	/// A non-success response becomes the exception the retry policy is built around, carrying the status code so
	/// the user can be told which kind of failure it was.
	/// </summary>
	[Fact]
	public async Task GetAsync_UnknownPath_ThrowsHttpStatusException()
	{
		var settings = new StubApiSettings();
		var apiClient = new APIClient(settings, ApiSerializerSettings.Create(), SilentLogger.Create());

		var failure = await Should.ThrowAsync<HttpStatusException>(() =>
			apiClient.GetAsync<object>("no-such-collection"));

		((int)failure.StatusCode).ShouldBeGreaterThanOrEqualTo(400);
	}
}
