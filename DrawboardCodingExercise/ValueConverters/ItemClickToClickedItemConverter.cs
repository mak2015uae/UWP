using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace DrawboardCodingExercise.ValueConverters;

/// <summary>
/// Extracts the clicked row from a list view's item-click event arguments.
/// </summary>
/// <remarks>
/// Used as the input converter of an invoke-command action, so that a list's click gesture can be bound
/// straight to a ViewModel command with the row as its parameter. Without it, the command would receive the
/// event arguments themselves and the ViewModel would have to reference a platform type to make sense of them.
/// </remarks>
public class ItemClickToClickedItemConverter : IValueConverter
{
	/// <summary>
	/// Converts item-click event arguments into the clicked item.
	/// </summary>
	/// <param name="value">The event arguments raised by the list view.</param>
	/// <param name="targetType">The binding target type. Unused; the clicked item is passed through as-is.</param>
	/// <param name="parameter">The converter parameter. Unused.</param>
	/// <param name="language">The binding language. Unused.</param>
	/// <returns>
	/// The clicked item, or <see langword="null"/> when the arguments are of another type — which lets the
	/// bound command ignore the gesture rather than fail on it.
	/// </returns>
	public object Convert(object value, Type targetType, object parameter, string language) =>
		value is ItemClickEventArgs itemClick ? itemClick.ClickedItem : null;

	/// <summary>
	/// Not supported; a click gesture cannot be reconstructed from an item.
	/// </summary>
	/// <param name="value">Unused.</param>
	/// <param name="targetType">Unused.</param>
	/// <param name="parameter">Unused.</param>
	/// <param name="language">Unused.</param>
	/// <returns>This method never returns.</returns>
	/// <exception cref="NotSupportedException">Always thrown.</exception>
	public object ConvertBack(object value, Type targetType, object parameter, string language) =>
		throw new NotSupportedException("This is a one-way converter");
}
