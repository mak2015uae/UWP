using Serilog;
using Serilog.Core;

namespace DrawboardCodingExercise.TestSupport;

/// <summary>
/// Supplies a logger for code under test that writes nowhere.
/// </summary>
/// <remarks>
/// The classes under test take a real <see cref="ILogger"/> and call it freely. A substitute would work, but
/// every test would have to configure it; a silent real logger keeps the arrangement to one call and keeps test
/// output clean.
/// </remarks>
public static class SilentLogger
{
	/// <summary>
	/// Creates a logger that discards everything written to it.
	/// </summary>
	/// <returns>A logger with no sinks.</returns>
	public static ILogger Create() => Logger.None;
}
