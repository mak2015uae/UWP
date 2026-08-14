using System;
using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using DrawboardCodingExercise.Contracts;
using JetBrains.Annotations;

namespace DrawboardCodingExercise.Module;

/// <summary>
/// Utility extension methods to make registering View/ViewModel pairs easier.
/// </summary>
[UsedImplicitly]
public static class MvvmViewExtensions
{
	/// <summary>
	/// Registers a View and its ViewModel against a single page key.
	/// </summary>
	/// <typeparam name="TView">The page type. Registered as a <see cref="Type"/> because the frame navigates
	/// by type and constructs the page itself.</typeparam>
	/// <typeparam name="TViewModel">
	/// The ViewModel type. Must derive from <see cref="ObservableObject"/>, because that is the contract the
	/// navigation service resolves against — a ViewModel that does not will silently never be attached.
	/// </typeparam>
	/// <param name="builder">The container builder being configured.</param>
	/// <param name="pageKey">The key both halves are registered under.</param>
	/// <remarks>
	/// ViewModels are registered per dependency, so each navigation gets a fresh instance and any state that
	/// must outlive a navigation belongs in a shared service.
	/// <para>
	/// They are also registered as externally owned. A disposable component resolved from the root container is
	/// otherwise retained by the container until the application exits, so a ViewModel that implements
	/// <see cref="IDisposable"/> — as page ViewModels do, to release their cancellation source — would accumulate
	/// one unreachable-but-uncollectable instance per navigation. The navigation framework owns page ViewModel
	/// lifetime, not the container, so this makes that ownership explicit.
	/// </para>
	/// </remarks>
	public static void RegisterView<TView, TViewModel>(this ContainerBuilder builder, PageKey pageKey)
	{
		builder.RegisterType(typeof(TViewModel)).Keyed<ObservableObject>(pageKey).ExternallyOwned();
		builder.RegisterInstance(typeof(TView)).Keyed<Type>(pageKey);
	}
}
