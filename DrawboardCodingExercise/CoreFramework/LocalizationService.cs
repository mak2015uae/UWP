using DrawboardCodingExercise.Contracts.Services;

namespace DrawboardCodingExercise.CoreFramework;

/// <summary>
/// Localizes a key, formatting it as necessary.
/// </summary>
/// <remarks>
/// Resource names in the resource file are written with dots, while the platform resource loader addresses
/// nested resources with slashes, so keys are rewritten on the way through. A missing key produces a visible
/// placeholder rather than an exception, so an untranslated string shows up in testing without taking the page
/// down.
/// </remarks>
public class LocalizationService : ILocalizationService
{
	/// <inheritdoc />
	public string Translate(string key, params object[] parameters)
	{
		var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
		var localizedString = resourceLoader.GetString(key.Replace('.', '/'));
		return string.IsNullOrEmpty(localizedString)
			? $"[{key}]"
			: string.Format(localizedString, parameters);
	}
}
