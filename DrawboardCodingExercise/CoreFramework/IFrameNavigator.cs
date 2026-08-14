using Windows.UI.Xaml.Controls;

namespace DrawboardCodingExercise.CoreFramework;

/// <summary>
/// The FrameNavigator is responsible for delegating the control Frame present in a Shell, or perhaps a nested view.
/// </summary>
/// <remarks>
/// Separate from the navigation service's own contract so that only the shell — the component that actually owns
/// a frame — can hand one over, while everything else navigates by page key alone. Both are implemented by the
/// same single instance.
/// </remarks>
public interface IFrameNavigator
{
	/// <summary>
	/// Sets the frame that navigation will be performed against.
	/// </summary>
	/// <value>
	/// The hosting frame, assigned once during shell construction. Write-only by design: exposing a getter
	/// would let callers bypass the navigation service.
	/// </value>
	Frame Frame { set; }
}
