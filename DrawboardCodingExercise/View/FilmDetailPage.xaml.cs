namespace DrawboardCodingExercise.View;

/// <summary>
/// Shows a single film's details, its opening crawl, and the characters that appear in it.
/// </summary>
/// <remarks>
/// Intentionally free of logic. The navigation service assigns the data context and passes the film identifier
/// to the ViewModel's navigated-to hook.
/// </remarks>
public sealed partial class FilmDetailPage
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FilmDetailPage"/> class.
	/// </summary>
	public FilmDetailPage()
	{
		InitializeComponent();
	}
}
