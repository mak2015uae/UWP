using System.Globalization;
using System.Linq;
using DrawboardCodingExercise.Contracts.Services;

namespace DrawboardCodingExercise.TestSupport;

/// <summary>
/// An <see cref="ILocalizationService"/> that echoes the resource key instead of translating it.
/// </summary>
/// <remarks>
/// Lets tests assert on which string a ViewModel chose without coupling them to English wording that a
/// translator may legitimately change. Formatting parameters are appended so that argument substitution is
/// still observable.
/// </remarks>
public sealed class KeyEchoLocalizationService : ILocalizationService
{
	/// <inheritdoc />
	/// <returns>
	/// The key alone when no parameters are supplied, otherwise the key followed by the parameters in
	/// parentheses — for example <c>Film.EpisodeFormat(IV)</c>.
	/// </returns>
	public string Translate(string key, params object[] parameters) =>
		parameters is null || parameters.Length == 0
			? key
			: string.Format(
				CultureInfo.InvariantCulture,
				"{0}({1})",
				key,
				string.Join(",", parameters.Select(parameter => parameter?.ToString() ?? string.Empty)));
}
