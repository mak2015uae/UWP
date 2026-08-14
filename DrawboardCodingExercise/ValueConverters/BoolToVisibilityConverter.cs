using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace DrawboardCodingExercise.ValueConverters;

/// <summary>
/// Converts a boolean into a visibility, with both directions configurable per instance.
/// </summary>
/// <remarks>
/// Because the mapping is configurable, the same converter serves both "show when true" and "show when false"
/// bindings without a second inverted converter type.
/// </remarks>
public class BoolToVisibilityConverter : IValueConverter
{
	/// <summary>
	/// Gets or sets the visibility produced for <see langword="true"/>.
	/// </summary>
	public Visibility IfTrueThen { get; set; } = Visibility.Visible;

	/// <summary>
	/// Gets or sets the visibility produced for <see langword="false"/>, and for <see langword="null"/>.
	/// </summary>
	public Visibility IfFalseThen { get; set; } = Visibility.Collapsed;

	/// <summary>
	/// Converts a boolean into a visibility.
	/// </summary>
	/// <param name="value">The bound value. <see langword="null"/> is treated as <see langword="false"/>.</param>
	/// <param name="targetType">The binding target type. Unused.</param>
	/// <param name="parameter">The converter parameter. Unused.</param>
	/// <param name="language">The binding language. Unused.</param>
	/// <returns>
	/// <see cref="IfTrueThen"/> or <see cref="IfFalseThen"/>.
	/// </returns>
	/// <exception cref="NotSupportedException">
	/// <paramref name="value"/> is neither a boolean nor <see langword="null"/>, which means the binding is
	/// pointed at the wrong property — worth failing loudly during development.
	/// </exception>
	public object Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is null)
		{
			return IfFalseThen;
		}

		if (value is bool theBoolean)
		{
			return theBoolean ? IfTrueThen : IfFalseThen;
		}

		throw new NotSupportedException($"Attempted to convert an unsupported object of type {value.GetType()}");
	}

	/// <summary>
	/// Converts a visibility back into a boolean.
	/// </summary>
	/// <param name="value">The visibility to interpret.</param>
	/// <param name="targetType">The binding target type. Unused.</param>
	/// <param name="parameter">The converter parameter. Unused.</param>
	/// <param name="language">The binding language. Unused.</param>
	/// <returns><see langword="true"/> when <paramref name="value"/> equals <see cref="IfTrueThen"/>.</returns>
	public object ConvertBack(object value, Type targetType, object parameter, string language)
	{
		return Equals(value, IfTrueThen);
	}
}
