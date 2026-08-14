using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawboardCodingExercise.Contracts;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Navigation;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.ViewModel.Formatting;
using DrawboardCodingExercise.ViewModel.Infrastructure;
using DrawboardCodingExercise.ViewModel.Items;

namespace DrawboardCodingExercise.ViewModel;

/// <summary>
/// Backs the landing page: lists every film, and navigates to a film's detail page when a row is chosen.
/// </summary>
/// <remarks>
/// Loading is delegated to <see cref="IBusyOperationRunner"/>, which owns shell progress reporting and the
/// retry prompt, so this ViewModel only decides what to display for each outcome. Every state the page can be
/// in — loading, loaded, empty and failed — is expressed as bound properties so the view never has to infer
/// one from the absence of another.
/// </remarks>
public partial class FilmListViewModel : PageViewModelBase, INavigateToAware, IProvidePageHeader
{
	private readonly IFilmService _filmService;
	private readonly IBusyOperationRunner _operationRunner;
	private readonly INavigationService _navigationService;
	private readonly ILocalizationService _localizationService;

	/// <summary>The rows currently bound to the list, ordered by episode number.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasFilms), nameof(IsEmpty))]
	private IReadOnlyList<FilmListItemViewModel> _films = Array.Empty<FilmListItemViewModel>();

	/// <summary>
	/// The localized explanation shown when the list could not be retrieved, or <see langword="null"/> when
	/// there is nothing wrong.
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasError), nameof(IsEmpty))]
	private string? _errorMessage;

	/// <summary>
	/// Whether a load attempt has finished, successfully or otherwise. Distinguishes "nothing yet" from
	/// "nothing at all", which is what stops an empty-state message flashing up during the first load.
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsEmpty))]
	private bool _hasCompletedLoad;

	/// <summary>
	/// Initializes a new instance of the <see cref="FilmListViewModel"/> class.
	/// </summary>
	/// <param name="filmService">Supplies the films.</param>
	/// <param name="operationRunner">Reports progress and handles retryable failures.</param>
	/// <param name="navigationService">Performs the navigation to the detail page.</param>
	/// <param name="localizationService">Resolves the busy, empty and error text.</param>
	public FilmListViewModel(
		IFilmService filmService,
		IBusyOperationRunner operationRunner,
		INavigationService navigationService,
		ILocalizationService localizationService)
		: base(navigationService)
	{
		_filmService = filmService;
		_operationRunner = operationRunner;
		_navigationService = navigationService;
		_localizationService = localizationService;
	}

	/// <summary>
	/// Gets a value indicating whether there are rows to show.
	/// </summary>
	public bool HasFilms => Films.Count > 0;

	/// <summary>
	/// Gets a value indicating whether the page is showing a failure message.
	/// </summary>
	public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

	/// <summary>
	/// Gets a value indicating whether the page should show its empty state.
	/// </summary>
	/// <value>
	/// <see langword="true"/> only once a load has finished with no films and no error — the API genuinely
	/// returned nothing, which is different from a failure and is worth saying so.
	/// </value>
	public bool IsEmpty => HasCompletedLoad && !HasFilms && !HasError;

	/// <inheritdoc />
	public string PageHeader => "FilmList";

	/// <inheritdoc />
	/// <remarks>
	/// Ignores its parameter: the landing page takes none. A cancelled outcome leaves the page silent, because
	/// cancellation only happens when the user has already navigated somewhere else.
	/// </remarks>
	public async Task OnNavigatedToAsync(object? parameter)
	{
		BeginPageLifetime();

		ErrorMessage = null;

		var outcome = await _operationRunner.RunAsync(
			_localizationService.Translate("Films.Loading"),
			token => _filmService.GetFilmsAsync(token),
			PageLifetimeToken).ConfigureAwait(true);

		if (outcome.IsSuccessful)
		{
			Films = outcome.Value
				.Select(film => new FilmListItemViewModel(
					film.Id,
					film.Title,
					_localizationService.Translate(
						"Film.EpisodeFormat",
						EpisodeFormatter.ToRomanNumeral(film.EpisodeNumber))))
				.ToList();
		}
		else if (outcome.FailureReason != OperationFailureReason.Cancelled)
		{
			Films = Array.Empty<FilmListItemViewModel>();
			ErrorMessage = _localizationService.Translate("Films.LoadFailed");
		}

		HasCompletedLoad = outcome.FailureReason != OperationFailureReason.Cancelled;
	}

	/// <summary>
	/// Opens the detail page for the chosen film.
	/// </summary>
	/// <param name="film">
	/// The chosen row. Ignored when <see langword="null"/>, which the item-click plumbing can produce if a
	/// click lands outside a row.
	/// </param>
	/// <remarks>
	/// Passes a <see cref="FilmDetailParameter"/> rather than the row itself, so the value replays correctly
	/// when the user comes back to this page and then forward again.
	/// </remarks>
	[RelayCommand]
	private async Task OnFilmSelected(FilmListItemViewModel? film)
	{
		if (film is null)
		{
			return;
		}

		await _navigationService
			.NavigateAsync(PageKey.FilmDetail, new FilmDetailParameter(film.FilmId))
			.ConfigureAwait(true);
	}
}
