using DrawboardCodingExercise.ViewModel.Formatting;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.ViewModel.UnitTests;

/// <summary>
/// Tests for <see cref="EpisodeFormatter"/>.
/// </summary>
public class EpisodeFormatterTests
{
	/// <summary>
	/// The episode numbers the films actually use, plus the boundaries where numeral construction changes.
	/// </summary>
	/// <param name="episodeNumber">The number to format.</param>
	/// <param name="expected">The expected numeral.</param>
	[Theory]
	[InlineData(1, "I")]
	[InlineData(3, "III")]
	[InlineData(4, "IV")]
	[InlineData(5, "V")]
	[InlineData(6, "VI")]
	[InlineData(9, "IX")]
	[InlineData(10, "X")]
	[InlineData(14, "XIV")]
	[InlineData(40, "XL")]
	[InlineData(90, "XC")]
	[InlineData(400, "CD")]
	[InlineData(900, "CM")]
	[InlineData(1987, "MCMLXXXVII")]
	[InlineData(3999, "MMMCMXCIX")]
	public void ToRomanNumeral_ValueInRange_ProducesTheNumeral(int episodeNumber, string expected)
	{
		EpisodeFormatter.ToRomanNumeral(episodeNumber).ShouldBe(expected);
	}

	/// <summary>
	/// Values Roman numerals cannot express fall back to digits rather than throwing, so an unexpected value from
	/// the API still renders something truthful instead of taking the page down.
	/// </summary>
	/// <param name="episodeNumber">The out-of-range number.</param>
	/// <param name="expected">The expected digit fallback.</param>
	[Theory]
	[InlineData(0, "0")]
	[InlineData(-3, "-3")]
	[InlineData(4000, "4000")]
	public void ToRomanNumeral_ValueOutOfRange_FallsBackToDigits(int episodeNumber, string expected)
	{
		EpisodeFormatter.ToRomanNumeral(episodeNumber).ShouldBe(expected);
	}
}
