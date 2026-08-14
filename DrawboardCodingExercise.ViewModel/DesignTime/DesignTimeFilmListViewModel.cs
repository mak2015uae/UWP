using System.Collections.Generic;
using DrawboardCodingExercise.ViewModel.Items;
using JetBrains.Annotations;

namespace DrawboardCodingExercise.ViewModel.DesignTime;

/// <summary>
/// A parameterless <see cref="FilmListViewModel"/> pre-filled with sample rows, so the XAML designer shows a
/// populated list instead of an empty page.
/// </summary>
/// <remarks>
/// The designer cannot resolve constructor dependencies from the container, so nulls are passed to the base
/// constructor. Only the bound properties are touched at design time, never the services.
/// </remarks>
[UsedImplicitly]
public class DesignTimeFilmListViewModel : FilmListViewModel
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DesignTimeFilmListViewModel"/> class.
	/// </summary>
	public DesignTimeFilmListViewModel() : base(null!, null!, null!, null!)
	{
		Films = new List<FilmListItemViewModel>
		{
			new(1, "A New Hope", "Episode IV"),
			new(2, "The Empire Strikes Back", "Episode V"),
			new(3, "Return of the Jedi", "Episode VI")
		};

		HasCompletedLoad = true;
	}
}
