using DrawboardCodingExercise.Services;

namespace DrawboardCodingExercise.Configuration;

/// <summary>
/// The application configuration, probably stored in LocalAppData normally.
/// </summary>
/// <remarks>
/// Hardcoded here because the exercise needs no configuration storage. It is registered as a single instance
/// against <see cref="IAPISettings"/>, so pointing the application at a different environment — or at a local
/// stub during manual failure testing — is a one-line change in one place.
/// </remarks>
public class ApplicationConfiguration : IAPISettings
{
	/// <inheritdoc />
	/// <remarks>
	/// Includes the <c>/api</c> path prefix shared by every endpoint, because request paths are supplied
	/// relative to this value. The trailing slash also makes the base address unambiguous when absolute
	/// resource URLs from payloads are converted back into relative paths.
	/// </remarks>
	public string ServerAddress { get; } = "https://swapi.info/api/";
}
