namespace DrawboardCodingExercise.Services;

/// <summary>
/// Supplies the settings the API client needs to reach the server.
/// </summary>
public interface IAPISettings
{
	/// <summary>
	/// Gets the absolute base address every request path is resolved against.
	/// </summary>
	/// <value>
	/// The API root, for example <c>https://swapi.info/api/</c>. A trailing slash is optional — the client
	/// normalizes it — but the value must include any path prefix shared by all endpoints, because request
	/// paths are supplied relative to it.
	/// </value>
	string ServerAddress { get; }
}
