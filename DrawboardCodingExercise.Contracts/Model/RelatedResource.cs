namespace DrawboardCodingExercise.Contracts.Model;

/// <summary>
/// A single resource related to a film, reduced to the parts the UI presents.
/// </summary>
/// <param name="Name">
/// The resource's display name. Never <see langword="null"/>; an empty string when the API supplies no name.
/// </param>
/// <param name="Kind">The category this resource was resolved from.</param>
/// <param name="Url">
/// The absolute resource URL it was resolved from, retained as a stable identity for caching and
/// de-duplication.
/// </param>
public sealed record RelatedResource(string Name, RelatedResourceKind Kind, string Url);
