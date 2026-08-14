using Newtonsoft.Json;

namespace DrawboardCodingExercise.Services.Api.Dto;

/// <summary>
/// The wire format of a single film, as returned by the films endpoint.
/// </summary>
/// <remarks>
/// The endpoint returns a bare JSON array of these objects rather than an envelope, so callers deserialize
/// into an array of this type. Field names are snake_case, which the application's camel-case contract
/// resolver does not map, so every affected member declares an explicit <see cref="JsonPropertyAttribute"/>
/// name. This type stays internal to the services layer; callers consume
/// <see cref="Contracts.Model.Film"/> instead.
/// <para>
/// Every member is nullable because a public API may omit or null any field; the mapper is responsible for
/// substituting safe defaults.
/// </para>
/// </remarks>
internal sealed class FilmDto
{
	/// <summary>Gets or sets the film's title.</summary>
	[JsonProperty("title")]
	public string? Title { get; set; }

	/// <summary>Gets or sets the episode number, mapped from <c>episode_id</c>.</summary>
	[JsonProperty("episode_id")]
	public int EpisodeId { get; set; }

	/// <summary>
	/// Gets or sets the opening crawl, mapped from <c>opening_crawl</c>. Contains embedded line breaks.
	/// </summary>
	[JsonProperty("opening_crawl")]
	public string? OpeningCrawl { get; set; }

	/// <summary>Gets or sets the credited director.</summary>
	[JsonProperty("director")]
	public string? Director { get; set; }

	/// <summary>
	/// Gets or sets the credited producers as a single string, which may list several comma-separated names.
	/// </summary>
	[JsonProperty("producer")]
	public string? Producer { get; set; }

	/// <summary>
	/// Gets or sets the release date, mapped from <c>release_date</c> as an ISO <c>yyyy-MM-dd</c> string.
	/// </summary>
	[JsonProperty("release_date")]
	public string? ReleaseDate { get; set; }

	/// <summary>Gets or sets the absolute URLs of the people appearing in the film.</summary>
	[JsonProperty("characters")]
	public string?[]? Characters { get; set; }

	/// <summary>Gets or sets the absolute URLs of the planets appearing in the film.</summary>
	[JsonProperty("planets")]
	public string?[]? Planets { get; set; }

	/// <summary>Gets or sets the absolute URLs of the starships appearing in the film.</summary>
	[JsonProperty("starships")]
	public string?[]? Starships { get; set; }

	/// <summary>Gets or sets the absolute URLs of the vehicles appearing in the film.</summary>
	[JsonProperty("vehicles")]
	public string?[]? Vehicles { get; set; }

	/// <summary>Gets or sets the absolute URLs of the species appearing in the film.</summary>
	[JsonProperty("species")]
	public string?[]? Species { get; set; }

	/// <summary>
	/// Gets or sets this film's own absolute resource URL, whose trailing segment supplies the identifier.
	/// </summary>
	[JsonProperty("url")]
	public string? Url { get; set; }
}
