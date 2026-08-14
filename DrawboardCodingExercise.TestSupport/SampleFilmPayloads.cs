namespace DrawboardCodingExercise.TestSupport;

/// <summary>
/// Canned API payloads mirroring the live wire format, for tests that need to exercise deserialization.
/// </summary>
/// <remarks>
/// Written to match the real endpoint's shape as observed against the live service: a bare JSON array rather
/// than an envelope, snake_case field names, and related resources given as absolute URLs. Hand-written
/// fixtures that "look about right" are exactly how a property-naming defect survives a green test suite, so
/// these deliberately keep the awkward parts — the underscores, the absolute URLs, and the carriage returns in
/// the crawl.
/// </remarks>
public static class SampleFilmPayloads
{
	/// <summary>
	/// The path the films collection is requested from, relative to the base address.
	/// </summary>
	public const string FilmsPath = "films";

	/// <summary>
	/// Two films, deliberately listed newest-episode-first so ordering is actually tested, with mixed line
	/// endings in the crawl.
	/// </summary>
	public const string TwoFilms = @"[
  {
    ""title"": ""The Empire Strikes Back"",
    ""episode_id"": 5,
    ""opening_crawl"": ""It is a dark time for the\r\nRebellion."",
    ""director"": ""Irvin Kershner"",
    ""producer"": ""Gary Kurtz, Rick McCallum"",
    ""release_date"": ""1980-05-17"",
    ""characters"": [
      ""https://swapi.info/api/people/1"",
      ""https://swapi.info/api/people/2""
    ],
    ""planets"": [ ""https://swapi.info/api/planets/4"" ],
    ""starships"": [],
    ""vehicles"": [],
    ""species"": [ ""https://swapi.info/api/species/1"" ],
    ""url"": ""https://swapi.info/api/films/2""
  },
  {
    ""title"": ""A New Hope"",
    ""episode_id"": 4,
    ""opening_crawl"": ""It is a period of civil war.\r\nRebel spaceships, striking\r\nfrom a hidden base.\r\n\r\nDuring the battle, Rebel\r\nspies managed to steal plans."",
    ""director"": ""George Lucas"",
    ""producer"": ""Gary Kurtz, Rick McCallum"",
    ""release_date"": ""1977-05-25"",
    ""characters"": [
      ""https://swapi.info/api/people/1"",
      ""https://swapi.info/api/people/2"",
      ""https://swapi.info/api/people/3""
    ],
    ""planets"": [ ""https://swapi.info/api/planets/1"" ],
    ""starships"": [ ""https://swapi.info/api/starships/2"" ],
    ""vehicles"": [ ""https://swapi.info/api/vehicles/4"" ],
    ""species"": [ ""https://swapi.info/api/species/1"" ],
    ""url"": ""https://swapi.info/api/films/1""
  }
]";

	/// <summary>
	/// One film exercising every defensive path in the mapper: absent arrays, explicit nulls, a blank entry
	/// among the URLs, an unparsable date, and a resource URL with a trailing slash.
	/// </summary>
	public const string AwkwardFilm = @"[
  {
    ""title"": null,
    ""episode_id"": 9,
    ""opening_crawl"": null,
    ""director"": null,
    ""producer"": null,
    ""release_date"": ""not-a-date"",
    ""characters"": [ ""https://swapi.info/api/people/7"", null, ""   "" ],
    ""url"": ""https://swapi.info/api/films/9/""
  }
]";

	/// <summary>
	/// An empty films collection, for the empty-state path.
	/// </summary>
	public const string NoFilms = "[]";

	/// <summary>
	/// Builds a related-resource response body.
	/// </summary>
	/// <param name="name">The display name the resource should report.</param>
	/// <param name="url">The resource's own absolute URL.</param>
	/// <returns>A JSON object in the shape every related resource returns.</returns>
	public static string NamedResource(string name, string url) =>
		$@"{{ ""name"": ""{name}"", ""url"": ""{url}"" }}";
}
