using System.Globalization;
using System.Text;

namespace DrawboardCodingExercise.ViewModel.Formatting;

/// <summary>
/// Formats episode numbers the way the films themselves title them.
/// </summary>
/// <remarks>
/// A presentation concern, so it lives with the ViewModels rather than in the services layer, and it is a
/// plain static helper rather than a XAML value converter so that it is unit-testable without a UI host and
/// so both pages format episodes identically.
/// </remarks>
public static class EpisodeFormatter
{
	/// <summary>The largest value classical Roman numerals can express.</summary>
	private const int MaximumRomanNumeral = 3999;

	private static readonly (int Value, string Numeral)[] NumeralsDescending =
	{
		(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
		(100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
		(10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
	};

	/// <summary>
	/// Converts an episode number to a Roman numeral.
	/// </summary>
	/// <param name="episodeNumber">The episode number to format.</param>
	/// <returns>
	/// The Roman numeral, for example <c>IV</c> for 4. Values Roman numerals cannot express — zero, negatives
	/// and anything above 3999 — fall back to invariant digits rather than throwing, so an unexpected value
	/// from the API still renders something truthful.
	/// </returns>
	public static string ToRomanNumeral(int episodeNumber)
	{
		if (episodeNumber < 1 || episodeNumber > MaximumRomanNumeral)
		{
			return episodeNumber.ToString(CultureInfo.InvariantCulture);
		}

		var numeral = new StringBuilder();
		var remainder = episodeNumber;

		foreach (var (value, symbol) in NumeralsDescending)
		{
			while (remainder >= value)
			{
				numeral.Append(symbol);
				remainder -= value;
			}
		}

		return numeral.ToString();
	}
}
