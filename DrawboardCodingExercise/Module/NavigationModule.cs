using Autofac;
using DrawboardCodingExercise.Contracts;
using DrawboardCodingExercise.View;
using DrawboardCodingExercise.ViewModel;
using JetBrains.Annotations;

namespace DrawboardCodingExercise.Module;

/// <summary>
/// Defines the relationships between Views, ViewModels and their PageKeys.
/// A PageKey can only represent one page.
/// </summary>
[UsedImplicitly]
public class NavigationModule : Autofac.Module
{
	/// <summary>
	/// Registers the shell and every navigable page.
	/// </summary>
	/// <param name="builder">The container builder being configured.</param>
	protected override void Load(ContainerBuilder builder)
	{
		//Special handling for the shell as it's not navigated to, but constructed on application start.
		builder.RegisterType<Shell>().AsSelf();
		builder.RegisterType<ShellViewModel>().AsSelf();

		builder.RegisterView<FilmListPage, FilmListViewModel>(PageKey.FilmList);
		builder.RegisterView<FilmDetailPage, FilmDetailViewModel>(PageKey.FilmDetail);
	}
}
