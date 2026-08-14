using System;
using System.Collections.Generic;
using System.Linq;
using DrawboardCodingExercise.Contracts.Model;

namespace DrawboardCodingExercise.ViewModel.UnitTests;

/// <summary>
/// Builds domain films for ViewModel tests.
/// </summary>
/// <remarks>
/// ViewModel tests work against the domain model, not the wire format, so they build films directly instead of
/// going through deserialization — that boundary is covered by the services tests.
/// </remarks>
internal static class TestFilms
{
	/// <summary>
	/// Builds a film, with every field defaulted to something harmless so each test can name only what it cares
	/// about.
	/// </summary>
	/// <param name="id">The film identifier.</param>
	/// <param name="title">The title.</param>
	/// <param name="episodeNumber">The episode number.</param>
	/// <param name="releaseDate">The release date, or <see langword="null"/> to exercise the placeholder path.</param>
	/// <param name="director">The credited director.</param>
	/// <param name="producer">The credited producers.</param>
	/// <param name="openingCrawl">The opening crawl text.</param>
	/// <param name="characterUrls">The absolute character URLs the film references.</param>
	/// <returns>The constructed film.</returns>
	public static Film Create(
		int id = 1,
		string title = "A New Hope",
		int episodeNumber = 4,
		DateTimeOffset? releaseDate = null,
		string director = "George Lucas",
		string producer = "Gary Kurtz, Rick McCallum",
		string openingCrawl = "It is a period of civil war.",
		IReadOnlyList<string>? characterUrls = null) =>
		new(
			id,
			title,
			episodeNumber,
			releaseDate ?? new DateTimeOffset(1977, 5, 25, 0, 0, 0, TimeSpan.Zero),
			director,
			producer,
			openingCrawl,
			BuildRelatedUrls(characterUrls ?? Array.Empty<string>()));

	/// <summary>
	/// Builds the character URLs for a number of sequential people resources.
	/// </summary>
	/// <param name="count">How many URLs to produce.</param>
	/// <returns>Absolute URLs in the API's format.</returns>
	public static IReadOnlyList<string> CharacterUrls(int count) =>
		Enumerable.Range(1, count).Select(index => $"https://swapi.info/api/people/{index}").ToList();

	/// <summary>
	/// Builds the related-resource dictionary, with every category present as the domain model guarantees.
	/// </summary>
	/// <param name="characterUrls">The character URLs; other categories are left empty.</param>
	/// <returns>A dictionary containing every category.</returns>
	private static IReadOnlyDictionary<RelatedResourceKind, IReadOnlyList<string>> BuildRelatedUrls(
		IReadOnlyList<string> characterUrls) =>
		new Dictionary<RelatedResourceKind, IReadOnlyList<string>>
		{
			[RelatedResourceKind.Characters] = characterUrls,
			[RelatedResourceKind.Planets] = Array.Empty<string>(),
			[RelatedResourceKind.Starships] = Array.Empty<string>(),
			[RelatedResourceKind.Vehicles] = Array.Empty<string>(),
			[RelatedResourceKind.Species] = Array.Empty<string>()
		};
}
