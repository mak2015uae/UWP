namespace DrawboardCodingExercise.Contracts.Model;

/// <summary>
/// A category of resource related to a film. Each value corresponds to one array of absolute resource
/// URLs on the film payload.
/// </summary>
/// <remarks>
/// Every category resolves to the same wire shape — an object carrying a display name — so adding a
/// category requires no change to the retrieval logic.
/// </remarks>
public enum RelatedResourceKind
{
	/// <summary>The people appearing in the film.</summary>
	Characters,

	/// <summary>The planets appearing in the film.</summary>
	Planets,

	/// <summary>The starships appearing in the film.</summary>
	Starships,

	/// <summary>The vehicles appearing in the film.</summary>
	Vehicles,

	/// <summary>The species appearing in the film.</summary>
	Species
}
