namespace DrawboardCodingExercise.Contracts;

/// <summary>
/// Identifies a navigable page. Each key maps to exactly one View/ViewModel pair, registered in the
/// application's navigation module and resolved by the navigation service at navigation time.
/// </summary>
/// <remarks>
/// Adding a value here is only the first of four steps: the pair must also be registered with
/// <c>RegisterView</c>, the XAML and code-behind must be listed in the UWP project file, and a
/// <c>PageHeader.&lt;Key&gt;.Text</c> resource must exist for the page header converter.
/// </remarks>
public enum PageKey
{
	/// <summary>
	/// The landing page, listing every film returned by the films endpoint.
	/// </summary>
	FilmList,

	/// <summary>
	/// The detail page for a single film, navigated to with a
	/// <see cref="Navigation.FilmDetailParameter"/>.
	/// </summary>
	FilmDetail
}
