using System;
using DrawboardCodingExercise.Contracts.CoreFramework;

namespace DrawboardCodingExercise.ViewModel.Infrastructure;

/// <summary>
/// An <see cref="IProgress{T}"/> that delivers every report on the UI thread.
/// </summary>
/// <remarks>
/// Preferred over <see cref="Progress{T}"/>, which captures whatever synchronization context happened to be
/// current when it was constructed. Routing through <see cref="IThreadDispatcher"/> instead makes the
/// behaviour explicit, keeps it correct no matter which thread constructed the instance, and lets tests supply
/// a dispatcher that runs callbacks inline so incremental updates are deterministic.
/// </remarks>
/// <typeparam name="T">The type of progress value reported.</typeparam>
public sealed class DispatchedProgress<T> : IProgress<T>
{
	private readonly IThreadDispatcher _threadDispatcher;
	private readonly Action<T> _onReport;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchedProgress{T}"/> class.
	/// </summary>
	/// <param name="threadDispatcher">Marshals each report onto the UI thread.</param>
	/// <param name="onReport">
	/// The handler invoked for each report, always on the UI thread, so it may safely touch bound collections.
	/// </param>
	public DispatchedProgress(IThreadDispatcher threadDispatcher, Action<T> onReport)
	{
		_threadDispatcher = threadDispatcher;
		_onReport = onReport;
	}

	/// <inheritdoc />
	/// <remarks>
	/// Dispatch is fire-and-forget, so the handler may not have run by the time this method returns. Producers
	/// must not assume a report has been applied.
	/// </remarks>
	public void Report(T value) => _threadDispatcher.FireOnUIAndForget(() => _onReport(value));
}
