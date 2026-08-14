namespace DrawboardCodingExercise.Contracts.CoreFramework;

/// <summary>
/// Implemented by a page ViewModel that supplies the title shown in the shell's title bar.
/// </summary>
public interface IProvidePageHeader
{
	/// <summary>
	/// Gets the header's resource key fragment — not display text.
	/// </summary>
	/// <value>
	/// A fragment resolved by the shell against <c>PageHeader/&lt;value&gt;/Text</c> in the string resources.
	/// A fragment with no matching resource renders as a visible placeholder rather than throwing.
	/// </value>
	string PageHeader { get; }
}
