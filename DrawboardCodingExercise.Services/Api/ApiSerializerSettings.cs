using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DrawboardCodingExercise.Services.Api;

/// <summary>
/// Supplies the JSON serializer settings every API call uses.
/// </summary>
/// <remarks>
/// Defined here rather than inline in the container registration so that tests deserialize through exactly the
/// same configuration the application does. Duplicating the settings in a test would let the two drift, and a
/// property-naming mismatch is precisely the kind of defect these settings decide.
/// </remarks>
public static class ApiSerializerSettings
{
	/// <summary>
	/// Creates the serializer settings used for every request and response body.
	/// </summary>
	/// <returns>
	/// Fresh settings using camel-case property naming. Note that camel casing does not cover snake_case wire
	/// formats, so a payload using underscores still needs explicit property names on its data transfer object.
	/// </returns>
	/// <remarks>
	/// Returns a new instance per call because the settings object is mutable and Newtonsoft caches state
	/// against the contract resolver; sharing one instance across differently-configured consumers invites
	/// surprises.
	/// </remarks>
	public static JsonSerializerSettings Create() =>
		new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver(),
			Formatting = Formatting.Indented
		};
}
