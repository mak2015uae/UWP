using DrawboardCodingExercise.Services;

namespace DrawboardCodingExercise.TestSupport;

/// <summary>
/// Supplies a fixed base address to code under test.
/// </summary>
public sealed class StubApiSettings : IAPISettings
{
	/// <summary>
	/// The base address used by tests, matching the shape the application is configured with — including the
	/// path prefix and trailing slash, because both affect how absolute resource URLs are made relative.
	/// </summary>
	public const string DefaultServerAddress = "https://swapi.info/api/";

	/// <summary>
	/// Initializes a new instance of the <see cref="StubApiSettings"/> class.
	/// </summary>
	/// <param name="serverAddress">
	/// The base address to report. Defaults to <see cref="DefaultServerAddress"/>.
	/// </param>
	public StubApiSettings(string serverAddress = DefaultServerAddress) => ServerAddress = serverAddress;

	/// <inheritdoc />
	public string ServerAddress { get; }
}
