using System;
using System.Threading.Tasks;
using Windows.UI.Popups;
using DrawboardCodingExercise.Contracts.Services;

namespace DrawboardCodingExercise.CoreFramework;

/// <summary>
/// Presents modal prompts using the platform's message dialog.
/// </summary>
public class UserInteractionService : IUserInteractionService
{
	private readonly ILocalizationService _localizationService;

	/// <summary>
	/// Initializes a new instance of the <see cref="UserInteractionService"/> class.
	/// </summary>
	/// <param name="localizationService">Resolves the default message and the button captions.</param>
	public UserInteractionService(ILocalizationService localizationService)
	{
		_localizationService = localizationService;
	}

	/// <inheritdoc />
	public Task<RetryDialogResult> ShowRetryDialogAsync() =>
		ShowRetryDialogAsync(_localizationService.Translate("Errors.Retry"));

	/// <inheritdoc />
	public async Task<RetryDialogResult> ShowRetryDialogAsync(string message)
	{
		var dialog = new MessageDialog(message);

		var dialogResult = RetryDialogResult.Retry;

		IUICommand retryCommand = new UICommand(_localizationService.Translate("Dialog.RetryButton.Text"), command => dialogResult = RetryDialogResult.Retry);
		IUICommand cancelCommand = new UICommand(_localizationService.Translate("Dialog.CancelButton.Text"), command => dialogResult = RetryDialogResult.Cancel);

		dialog.Commands.Add(retryCommand);
		dialog.Commands.Add(cancelCommand);

		// Pressing Escape or otherwise dismissing the dialog resolves to Cancel rather than a silent retry loop.
		dialog.CancelCommandIndex = 1;

		await dialog.ShowAsync();

		return dialogResult;
	}
}
