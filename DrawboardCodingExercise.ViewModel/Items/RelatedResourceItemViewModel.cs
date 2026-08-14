namespace DrawboardCodingExercise.ViewModel.Items;

/// <summary>
/// One row in a film's related-resource list, such as a character.
/// </summary>
/// <remarks>
/// Immutable and notification-free for the same reasons as <see cref="FilmListItemViewModel"/>: rows are added
/// once as they resolve and never change afterwards.
/// </remarks>
/// <param name="Name">The resource's display name.</param>
/// <param name="Url">
/// The resource's absolute URL, retained as a stable identity so the page can place each row in the order the
/// API listed it, even though responses arrive out of order.
/// </param>
public sealed record RelatedResourceItemViewModel(string Name, string Url);
