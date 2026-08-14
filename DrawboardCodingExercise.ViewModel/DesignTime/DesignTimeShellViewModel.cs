using JetBrains.Annotations;

namespace DrawboardCodingExercise.ViewModel.DesignTime;

/// <summary>
/// A parameterless <see cref="ShellViewModel"/> for the XAML designer.
/// </summary>
/// <remarks>
/// The designer cannot resolve constructor dependencies from the container, so this passes nulls to the base
/// constructor. It is only ever instantiated at design time, where nothing invokes the members that would use
/// those dependencies.
/// </remarks>
[UsedImplicitly]
public class DesignTimeShellViewModel : ShellViewModel
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DesignTimeShellViewModel"/> class.
	/// </summary>
	public DesignTimeShellViewModel() : base(null!, null!)
	{
	}
}
