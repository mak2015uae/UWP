namespace DrawboardCodingExercise.Contracts.Navigation;

/// <summary>
/// The navigation parameter carried to the film detail page.
/// </summary>
/// <remarks>
/// Deliberately a value-only identifier rather than a <see cref="Model.Film"/> instance. The navigation
/// service stores the parameter in the frame's back stack and replays it on back navigation, and page
/// ViewModels are constructed fresh on every navigation, so an identifier that can be resolved against a
/// service is more robust than a captured object graph.
/// </remarks>
/// <param name="FilmId">The <see cref="Model.Film.Id"/> of the film to display.</param>
public sealed record FilmDetailParameter(int FilmId);
