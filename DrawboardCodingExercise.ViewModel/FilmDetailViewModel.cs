using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Contracts.Model;
using DrawboardCodingExercise.Contracts.Navigation;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.ViewModel.Formatting;
using DrawboardCodingExercise.ViewModel.Infrastructure;
using DrawboardCodingExercise.ViewModel.Items;

namespace DrawboardCodingExercise.ViewModel;

/// <summary>
/// Backs the film detail page: the film's own fields, its opening crawl, and the list of characters that
/// appear in it.
/// </summary>
/// <remarks>
/// The film's own details cost no network call — the films endpoint returns every field this page shows, so the
/// film service serves it from cache. Only the characters require requests, one per character, which arrive out
/// of order and are placed into the bound collection at their API-listed position as they resolve. The two
/// loads are run as separate guarded operations on purpose: a failure to resolve characters must not cost the
/// user the film details they can already be shown.
/// </remarks>
public partial class FilmDetailViewModel : PageViewModelBase, INavigateToAware, IProvidePageHeader
{
	/// <summary>The category of related resource this page lists.</summary>
	/// <remarks>
	/// A single constant rather than a hard-coded reference throughout, so exposing a category selector later
	/// means turning this into a bound property and changing nothing else.
	/// </remarks>
	private const RelatedResourceKind ListedResourceKind = RelatedResourceKind.Characters;

	private readonly IFilmService _filmService;
	private readonly IBusyOperationRunner _operationRunner;
	private readonly IThreadDispatcher _threadDispatcher;
	private readonly ILocalizationService _localizationService;

	/// <summary>
	/// The listed resources, kept in the order the API supplied them as the responses arrive out of order.
	/// </summary>
	private readonly SourceOrderedCollection<RelatedResourceItemViewModel> _relatedResources =
		new(resource => resource.Url);

	/// <summary>The film's title.</summary>
	[ObservableProperty]
	private string _title = string.Empty;

	/// <summary>The formatted episode caption, for example <c>Episode IV</c>.</summary>
	[ObservableProperty]
	private string _episodeLabel = string.Empty;

	/// <summary>
	/// The release date formatted for the current culture, or a localized placeholder when the API supplied no
	/// usable date.
	/// </summary>
	[ObservableProperty]
	private string _releaseDate = string.Empty;

	/// <summary>The credited director.</summary>
	[ObservableProperty]
	private string _director = string.Empty;

	/// <summary>The credited producers, as the API supplies them.</summary>
	[ObservableProperty]
	private string _producer = string.Empty;

	/// <summary>The opening crawl text, with line endings already normalized for display.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasOpeningCrawl))]
	private string _openingCrawl = string.Empty;

	/// <summary>Whether a film was found and its details are bound.</summary>
	[ObservableProperty]
	private bool _hasFilm;

	/// <summary>
	/// The localized explanation shown when the film itself could not be shown, or <see langword="null"/> when
	/// there is nothing wrong.
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasError))]
	private string? _errorMessage;

	/// <summary>
	/// The localized explanation shown when the related-resource list failed, while the film's own details
	/// remain on screen.
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasRelatedResourceError))]
	private string? _relatedResourceErrorMessage;

	/// <summary>Whether the related-resource load has finished, successfully or otherwise.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasNoRelatedResources))]
	private bool _hasCompletedRelatedResourceLoad;

	/// <summary>
	/// Initializes a new instance of the <see cref="FilmDetailViewModel"/> class.
	/// </summary>
	/// <param name="filmService">Supplies the film and its related resources.</param>
	/// <param name="operationRunner">Reports progress and handles retryable failures.</param>
	/// <param name="navigationService">Supplies this page's lifetime, for cancellation on navigating away.</param>
	/// <param name="threadDispatcher">Marshals incremental related-resource updates onto the UI thread.</param>
	/// <param name="localizationService">Resolves the busy, placeholder and error text.</param>
	public FilmDetailViewModel(
		IFilmService filmService,
		IBusyOperationRunner operationRunner,
		INavigationService navigationService,
		IThreadDispatcher threadDispatcher,
		ILocalizationService localizationService)
		: base(navigationService)
	{
		_filmService = filmService;
		_operationRunner = operationRunner;
		_threadDispatcher = threadDispatcher;
		_localizationService = localizationService;
	}

	/// <summary>
	/// Gets the listed related resources, filled incrementally as each one resolves and held in the order the API
	/// listed them.
	/// </summary>
	/// <remarks>
	/// Only ever modified on the UI thread, via the dispatcher, because a bound observable collection cannot be
	/// mutated from a worker thread.
	/// </remarks>
	public ObservableCollection<RelatedResourceItemViewModel> RelatedResources => _relatedResources.Items;

	/// <summary>Gets a value indicating whether this film has an opening crawl to show.</summary>
	public bool HasOpeningCrawl => !string.IsNullOrWhiteSpace(OpeningCrawl);

	/// <summary>Gets a value indicating whether the page is showing a failure message instead of a film.</summary>
	public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

	/// <summary>Gets a value indicating whether the related-resource list failed to load.</summary>
	public bool HasRelatedResourceError => !string.IsNullOrEmpty(RelatedResourceErrorMessage);

	/// <summary>
	/// Gets a value indicating whether the related-resource list finished with nothing in it.
	/// </summary>
	public bool HasNoRelatedResources =>
		HasCompletedRelatedResourceLoad && RelatedResources.Count == 0 && !HasRelatedResourceError;

	/// <inheritdoc />
	public string PageHeader => "FilmDetail";

	/// <inheritdoc />
	/// <param name="parameter">
	/// Expected to be a <see cref="FilmDetailParameter"/>. Anything else, including
	/// <see langword="null"/>, is reported as a not-found state rather than throwing — a page should not crash
	/// because it was navigated to incorrectly.
	/// </param>
	public async Task OnNavigatedToAsync(object? parameter)
	{
		BeginPageLifetime();

		ErrorMessage = null;

		if (parameter is not FilmDetailParameter filmDetail)
		{
			ErrorMessage = _localizationService.Translate("FilmDetail.NotFound");
			return;
		}

		var outcome = await _operationRunner.RunAsync(
			_localizationService.Translate("FilmDetail.Loading"),
			token => _filmService.GetFilmAsync(filmDetail.FilmId, token),
			PageLifetimeToken).ConfigureAwait(true);

		if (!outcome.IsSuccessful)
		{
			if (outcome.FailureReason != OperationFailureReason.Cancelled)
			{
				ErrorMessage = _localizationService.Translate("FilmDetail.LoadFailed");
			}

			return;
		}

		var film = outcome.Value;

		if (film is null)
		{
			ErrorMessage = _localizationService.Translate("FilmDetail.NotFound");
			return;
		}

		Apply(film);

		await LoadRelatedResourcesAsync(film).ConfigureAwait(true);
	}

	/// <summary>
	/// Binds a film's own fields, which are already in hand and need no further requests.
	/// </summary>
	/// <param name="film">The film to display.</param>
	private void Apply(Film film)
	{
		Title = film.Title;
		EpisodeLabel = _localizationService.Translate(
			"Film.EpisodeFormat",
			EpisodeFormatter.ToRomanNumeral(film.EpisodeNumber));
		ReleaseDate = film.ReleaseDate.HasValue
			? film.ReleaseDate.Value.ToString("d", CultureInfo.CurrentCulture)
			: _localizationService.Translate("FilmDetail.ReleaseDateUnknown");
		Director = film.Director;
		Producer = film.Producer;
		OpeningCrawl = film.OpeningCrawl;

		_relatedResources.Reset(
			film.RelatedResourceUrls.TryGetValue(ListedResourceKind, out var urls) ? urls : null);

		HasFilm = true;
	}

	/// <summary>
	/// Resolves the listed related resources, filling the bound collection as each one arrives.
	/// </summary>
	/// <param name="film">The film whose related resources are resolved.</param>
	/// <returns>A task that completes when the load has finished or been abandoned.</returns>
	private async Task LoadRelatedResourcesAsync(Film film)
	{
		RelatedResourceErrorMessage = null;

		if (_relatedResources.SourceCount == 0)
		{
			HasCompletedRelatedResourceLoad = true;
			return;
		}

		var progress = new DispatchedProgress<RelatedResource>(
			_threadDispatcher,
			resource => _relatedResources.Add(new RelatedResourceItemViewModel(resource.Name, resource.Url)));

		var outcome = await _operationRunner.RunAsync(
			_localizationService.Translate("Characters.Loading"),
			token => _filmService.GetRelatedResourcesAsync(film, ListedResourceKind, progress, token),
			PageLifetimeToken).ConfigureAwait(true);

		if (!outcome.IsSuccessful && outcome.FailureReason != OperationFailureReason.Cancelled)
		{
			RelatedResourceErrorMessage = _localizationService.Translate("Characters.LoadFailed");
		}

		HasCompletedRelatedResourceLoad = outcome.FailureReason != OperationFailureReason.Cancelled;
	}
}
