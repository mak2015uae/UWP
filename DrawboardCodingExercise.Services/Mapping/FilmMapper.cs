using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DrawboardCodingExercise.Contracts.Model;
using DrawboardCodingExercise.Services.Api.Dto;

namespace DrawboardCodingExercise.Services.Mapping;

/// <summary>
/// Maps film payloads onto the domain model, absorbing every quirk of the wire format so that nothing
/// downstream has to.
/// </summary>
/// <remarks>
/// Hand-written rather than generated: the shape is small, and the mapping is where the interesting decisions
/// live — tolerating nulls, parsing an identifier out of a URL, and normalizing line endings. A mapping
/// generator would hide those. Nothing here throws on malformed input; a public API can return anything, and
/// a single bad field must not cost the user the whole list.
/// </remarks>
internal static class FilmMapper
{
	/// <summary>
	/// Maps a batch of film payloads, ordered for display.
	/// </summary>
	/// <param name="films">
	/// The deserialized payload. Tolerates <see langword="null"/>, and skips any null element.
	/// </param>
	/// <returns>
	/// The mapped films ordered by <see cref="Film.EpisodeNumber"/>. Never <see langword="null"/>.
	/// </returns>
	public static IReadOnlyList<Film> ToDomain(IEnumerable<FilmDto?>? films) =>
		films is null
			? Array.Empty<Film>()
			: films
				.Where(film => film is not null)
				.Select(film => ToDomain(film!))
				.OrderBy(film => film.EpisodeNumber)
				.ToList();

	/// <summary>
	/// Maps a single film payload.
	/// </summary>
	/// <param name="film">The deserialized film.</param>
	/// <returns>The mapped film, with safe defaults substituted for any missing field.</returns>
	public static Film ToDomain(FilmDto film) =>
		new(
			ParseIdentifier(film.Url, film.EpisodeId),
			film.Title ?? string.Empty,
			film.EpisodeId,
			ParseReleaseDate(film.ReleaseDate),
			film.Director ?? string.Empty,
			film.Producer ?? string.Empty,
			NormalizeCrawl(film.OpeningCrawl),
			BuildRelatedResourceUrls(film));

	/// <summary>
	/// Extracts the numeric identifier from a resource URL's trailing segment.
	/// </summary>
	/// <param name="url">The resource URL, which may be null, or carry a trailing slash.</param>
	/// <param name="fallback">
	/// The value to use when no numeric segment can be found — the episode number, which is also unique per
	/// film, so navigation still resolves the right record.
	/// </param>
	/// <returns>The parsed identifier, or <paramref name="fallback"/>.</returns>
	private static int ParseIdentifier(string? url, int fallback)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return fallback;
		}

		var segments = url!.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

		for (var index = segments.Length - 1; index >= 0; index--)
		{
			if (int.TryParse(segments[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var identifier))
			{
				return identifier;
			}
		}

		return fallback;
	}

	/// <summary>
	/// Parses a release date without ever throwing.
	/// </summary>
	/// <param name="releaseDate">The raw value, expected as <c>yyyy-MM-dd</c>.</param>
	/// <returns>
	/// The parsed date at zero offset, or <see langword="null"/> when the value is missing or unparsable, so
	/// the UI can show a placeholder rather than a misleading default date.
	/// </returns>
	private static DateTimeOffset? ParseReleaseDate(string? releaseDate)
	{
		if (string.IsNullOrWhiteSpace(releaseDate))
		{
			return null;
		}

		if (DateTime.TryParseExact(
				releaseDate!.Trim(),
				"yyyy-MM-dd",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var exact))
		{
			return new DateTimeOffset(exact, TimeSpan.Zero);
		}

		// Fall back to a lenient parse so an unexpected but valid format still reaches the UI.
		return DateTimeOffset.TryParse(
			releaseDate,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
			out var lenient)
			? lenient
			: null;
	}

	/// <summary>
	/// Prepares the opening crawl for display: normalizes line endings, and lets the prose reflow while keeping
	/// its paragraph breaks.
	/// </summary>
	/// <param name="text">The raw crawl, which may be null and may mix line-ending styles.</param>
	/// <returns>
	/// Paragraphs separated by a blank line, each a single run of text for the view to wrap, or an empty string.
	/// </returns>
	/// <remarks>
	/// The API hard-wraps the crawl at roughly thirty characters, which suits the fixed width of a cinema screen
	/// but not a resizable window: rendered verbatim it becomes a narrow ragged column that reads as a layout
	/// fault. Single line breaks are therefore treated as soft — replaced with spaces so the view wraps to
	/// whatever width it has — while blank lines are kept, because those are the author's paragraph breaks
	/// rather than an artefact of the source's line length.
	/// </remarks>
	private static string NormalizeCrawl(string? text)
	{
		if (text is null)
		{
			return string.Empty;
		}

		var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");

		// A newline with no newline either side is a wrap in the source, not a paragraph break.
		var reflowed = Regex.Replace(normalized, @"(?<!\n)\n(?!\n)", " ");

		// Collapse runs of blank lines to a single separator, and tidy the spaces the reflow introduced.
		reflowed = Regex.Replace(reflowed, @"\n{2,}", "\n\n");
		reflowed = Regex.Replace(reflowed, @"[ \t]{2,}", " ");

		return reflowed.Trim();
	}

	/// <summary>
	/// Groups a film's related resource URLs by category.
	/// </summary>
	/// <param name="film">The deserialized film.</param>
	/// <returns>
	/// A dictionary containing every <see cref="RelatedResourceKind"/> as a key, with blank and null entries
	/// filtered out, so callers never need a missing-key or null check.
	/// </returns>
	private static IReadOnlyDictionary<RelatedResourceKind, IReadOnlyList<string>> BuildRelatedResourceUrls(
		FilmDto film) =>
		new Dictionary<RelatedResourceKind, IReadOnlyList<string>>
		{
			[RelatedResourceKind.Characters] = Clean(film.Characters),
			[RelatedResourceKind.Planets] = Clean(film.Planets),
			[RelatedResourceKind.Starships] = Clean(film.Starships),
			[RelatedResourceKind.Vehicles] = Clean(film.Vehicles),
			[RelatedResourceKind.Species] = Clean(film.Species)
		};

	/// <summary>
	/// Removes null and blank entries from a URL array.
	/// </summary>
	/// <param name="urls">The raw array, which may itself be null.</param>
	/// <returns>The surviving trimmed URLs, in their original order.</returns>
	private static IReadOnlyList<string> Clean(string?[]? urls) =>
		urls is null
			? Array.Empty<string>()
			: urls
				.Where(url => !string.IsNullOrWhiteSpace(url))
				.Select(url => url!.Trim())
				.ToList();
}
