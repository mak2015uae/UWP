using Autofac;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.Services;
using DrawboardCodingExercise.Services.Api;
using JetBrains.Annotations;

namespace DrawboardCodingExercise.Module;

/// <summary>
/// Defines the services that talk to the remote API.
/// </summary>
[UsedImplicitly]
public class WebServicesModule : Autofac.Module
{
	/// <summary>
	/// Registers the API client, its serializer settings, and the film service built on top of them.
	/// </summary>
	/// <param name="builder">The container builder being configured.</param>
	protected override void Load(ContainerBuilder builder)
	{
		// Created by the services layer so that tests deserialize through exactly the same configuration.
		var jsonSettings = ApiSerializerSettings.Create();

		builder.RegisterInstance(jsonSettings).AsSelf();
		builder.RegisterType<APIClient>().As<IAPIClient>();

		// Single instance so the film cache is shared: page ViewModels are rebuilt on every navigation, and
		// the detail page is served entirely from what the list page already retrieved.
		builder.RegisterType<FilmService>().As<IFilmService>().SingleInstance();
	}
}
