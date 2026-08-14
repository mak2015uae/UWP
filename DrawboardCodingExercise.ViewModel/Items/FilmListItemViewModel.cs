namespace DrawboardCodingExercise.ViewModel.Items;

/// <summary>
/// One row in the film list: the display text a row shows, plus the identifier needed to navigate to it.
/// </summary>
/// <remarks>
/// Deliberately immutable and free of change notification. Rows are replaced wholesale when the list reloads
/// rather than mutated, which lets the item template use compiled one-time bindings — the cheapest binding
/// mode — and keeps the type trivially constructible in tests.
/// </remarks>
/// <param name="FilmId">
/// The film's identifier, used to build the detail page's navigation parameter.
/// </param>
/// <param name="Title">The film's title.</param>
/// <param name="EpisodeLabel">
/// The already-localized and formatted episode caption, for example <c>Episode IV</c>. Formatted by the page
/// ViewModel so that rows carry no service dependencies.
/// </param>
public sealed record FilmListItemViewModel(int FilmId, string Title, string EpisodeLabel);
