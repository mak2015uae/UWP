using DrawboardCodingExercise.CoreFramework;

namespace DrawboardCodingExercise.View;

/// <summary>
/// The root of the application, handles modal dialogs
/// </summary>
public sealed partial class Shell
{
	/// <summary>
	/// Initializes a new instance of the <see cref="Shell"/> class.
	/// </summary>
	/// <param name="frameNavigator">
	/// Receives this shell's frame. Handing it over during construction is what makes the navigation service
	/// usable before the first navigation is attempted.
	/// </param>
	public Shell(IFrameNavigator frameNavigator)
	{
		InitializeComponent();
		frameNavigator.Frame = NavFrame;
	}
}
