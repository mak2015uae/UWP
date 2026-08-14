namespace DrawboardCodingExercise.View;

/// <summary>
/// Lists every film, and opens a film's detail page when a row is clicked.
/// </summary>
/// <remarks>
/// Intentionally free of logic. The data context is assigned by the navigation service after construction, and
/// the item-click gesture reaches the ViewModel through a XAML behaviour rather than an event handler here.
/// </remarks>
public sealed partial class FilmListPage
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FilmListPage"/> class.
	/// </summary>
	public FilmListPage()
	{
		InitializeComponent();
	}
}
