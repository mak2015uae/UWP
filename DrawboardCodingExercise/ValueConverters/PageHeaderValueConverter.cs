using System;
using Windows.UI.Xaml.Data;
using DrawboardCodingExercise.Contracts.CoreFramework;

namespace DrawboardCodingExercise.ValueConverters;

/// <summary>
/// For Pages with a ViewModel that implements <see cref="IProvidePageHeader"/>, the header key provided by the ViewModel is localized
/// </summary>
/// <remarks>
/// Bound against the frame's live content, so the shell's title follows navigation without the shell knowing
/// which pages exist.
/// </remarks>
public class PageHeaderValueConverter : IValueConverter
{
	/// <summary>
	/// Resolves a page ViewModel's header key into localized title text.
	/// </summary>
	/// <param name="value">
	/// The current page's data context. Anything that does not implement <see cref="IProvidePageHeader"/>
	/// produces an empty title rather than an error.
	/// </param>
	/// <param name="targetType">The binding target type. Unused.</param>
	/// <param name="parameter">The converter parameter. Unused.</param>
	/// <param name="language">The binding language. Unused.</param>
	/// <returns>
	/// The localized title, or a visible <c>[PageHeader.Key.Text]</c> placeholder when the resource is missing,
	/// so an unlocalized page is obvious during testing.
	/// </returns>
	public object Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is IProvidePageHeader page)
		{
			var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView("Resources");
			var localizedString = resourceLoader.GetString($"PageHeader/{page.PageHeader}/Text");
			if (string.IsNullOrEmpty(localizedString))
			{
				return $"[PageHeader.{page.PageHeader}.Text]";
			}

			return localizedString;
		}

		return string.Empty;
	}

	/// <summary>
	/// Not supported; a localized title cannot be turned back into a ViewModel.
	/// </summary>
	/// <param name="value">Unused.</param>
	/// <param name="targetType">Unused.</param>
	/// <param name="parameter">Unused.</param>
	/// <param name="language">Unused.</param>
	/// <returns>This method never returns.</returns>
	/// <exception cref="NotSupportedException">Always thrown.</exception>
	public object ConvertBack(object value, Type targetType, object parameter, string language)
	{
		throw new NotSupportedException("This is a one-way converter");
	}
}
