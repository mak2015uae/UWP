using Newtonsoft.Json;

namespace DrawboardCodingExercise.Services.Api.Dto;

/// <summary>
/// The wire format of any resource related to a film, reduced to the fields the UI needs.
/// </summary>
/// <remarks>
/// People, planets, starships, vehicles and species all expose a <c>name</c>, so one shape covers every
/// category and adding a category needs no new type. Both field names are single words, so no explicit
/// property naming is required.
/// </remarks>
internal sealed class NamedResourceDto
{
	/// <summary>Gets or sets the resource's display name.</summary>
	[JsonProperty("name")]
	public string? Name { get; set; }

	/// <summary>Gets or sets the resource's own absolute URL.</summary>
	[JsonProperty("url")]
	public string? Url { get; set; }
}
