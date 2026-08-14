using DrawboardCodingExercise.ViewModel.Items;
using JetBrains.Annotations;

namespace DrawboardCodingExercise.ViewModel.DesignTime;

/// <summary>
/// A parameterless <see cref="FilmDetailViewModel"/> pre-filled with sample content, so the XAML designer can
/// lay out the detail page against realistic text lengths.
/// </summary>
/// <remarks>
/// The designer cannot resolve constructor dependencies from the container, so nulls are passed to the base
/// constructor. Only the bound properties are touched at design time, never the services.
/// </remarks>
[UsedImplicitly]
public class DesignTimeFilmDetailViewModel : FilmDetailViewModel
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DesignTimeFilmDetailViewModel"/> class.
	/// </summary>
	public DesignTimeFilmDetailViewModel() : base(null!, null!, null!, null!, null!)
	{
		Title = "A New Hope";
		EpisodeLabel = "Episode IV";
		ReleaseDate = "25/05/1977";
		Director = "George Lucas";
		Producer = "Gary Kurtz, Rick McCallum";
		OpeningCrawl =
			"It is a period of civil war.\nRebel spaceships, striking\nfrom a hidden base, have won\n" +
			"their first victory against\nthe evil Galactic Empire.";
		HasFilm = true;

		RelatedResources.Add(new RelatedResourceItemViewModel("Luke Skywalker", "https://swapi.info/api/people/1"));
		RelatedResources.Add(new RelatedResourceItemViewModel("C-3PO", "https://swapi.info/api/people/2"));
		RelatedResources.Add(new RelatedResourceItemViewModel("Darth Vader", "https://swapi.info/api/people/4"));

		HasCompletedRelatedResourceLoad = true;
	}
}
