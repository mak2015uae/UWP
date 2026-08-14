using System;
using System.Collections.Generic;

namespace DrawboardCodingExercise.Contracts.Model;

/// <summary>
/// An immutable projection of a single film, mapped from the films endpoint payload.
/// </summary>
/// <remarks>
/// The films endpoint returns every field required by both the list and the detail page, including
/// <see cref="OpeningCrawl"/>. Detail views are therefore served from the cached film list rather than a
/// second network call. Related-resource URLs are kept in the absolute form the API supplies; they must be
/// converted to a base-relative path before being passed to the API client.
/// </remarks>
/// <param name="Id">
/// The film's numeric identifier, parsed from the trailing segment of its resource URL. Used as the
/// navigation parameter for the detail page.
/// </param>
/// <param name="Title">The film's title as supplied by the API.</param>
/// <param name="EpisodeNumber">
/// The episode number. The films were released out of order, so this is not a release-chronology sort key.
/// </param>
/// <param name="ReleaseDate">
/// The cinematic release date, or <see langword="null"/> when the API omits it or supplies a value that
/// cannot be parsed. Callers show a localized placeholder rather than substituting a default date.
/// </param>
/// <param name="Director">The credited director.</param>
/// <param name="Producer">
/// The credited producers as the single string the API supplies; may contain several comma-separated names.
/// </param>
/// <param name="OpeningCrawl">
/// The opening crawl text, with line endings normalized to <c>\n</c> so it renders consistently in XAML.
/// </param>
/// <param name="RelatedResourceUrls">
/// Absolute resource URLs grouped by category. Every <see cref="RelatedResourceKind"/> is present as a key,
/// with an empty collection where the API listed none, so callers never need a null or missing-key check.
/// </param>
public sealed record Film(
	int Id,
	string Title,
	int EpisodeNumber,
	DateTimeOffset? ReleaseDate,
	string Director,
	string Producer,
	string OpeningCrawl,
	IReadOnlyDictionary<RelatedResourceKind, IReadOnlyList<string>> RelatedResourceUrls);
