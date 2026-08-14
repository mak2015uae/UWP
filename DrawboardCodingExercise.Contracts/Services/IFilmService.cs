using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.Model;

namespace DrawboardCodingExercise.Contracts.Services;

/// <summary>
/// Provides access to film data, insulating callers from transport, JSON and caching concerns.
/// </summary>
/// <remarks>
/// Implementations are registered as a single instance so the cache is shared across page navigations —
/// page ViewModels are rebuilt on every navigation, including back navigation, so without a shared cache
/// each visit would re-fetch. All members are safe to call from any thread and never marshal to the UI
/// thread; callers are responsible for dispatching UI updates through
/// <see cref="CoreFramework.IThreadDispatcher"/>.
/// </remarks>
public interface IFilmService
{
	/// <summary>
	/// Gets every film exposed by the API, ordered by <see cref="Film.EpisodeNumber"/>.
	/// </summary>
	/// <param name="cancellationToken">A token that abandons the request when the caller navigates away.</param>
	/// <returns>
	/// The complete film collection, served from cache once retrieved. Never <see langword="null"/>, and
	/// empty only when the API genuinely returns no films.
	/// </returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
	Task<IReadOnlyList<Film>> GetFilmsAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a single film by identifier, populating the cache first when it is cold.
	/// </summary>
	/// <param name="filmId">
	/// The identifier carried by <see cref="Navigation.FilmDetailParameter"/>.
	/// </param>
	/// <param name="cancellationToken">A token that abandons the request when the caller navigates away.</param>
	/// <returns>The matching film, or <see langword="null"/> when no film carries that identifier.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
	Task<Film?> GetFilmAsync(int filmId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Resolves the display names of one category of resources related to a film.
	/// </summary>
	/// <param name="film">The film whose related resource URLs are resolved.</param>
	/// <param name="kind">
	/// The category to resolve, for example <see cref="RelatedResourceKind.Characters"/>.
	/// </param>
	/// <param name="progress">
	/// Optional sink notified as each resource arrives, letting the UI fill a list incrementally instead of
	/// waiting for the whole batch. Invoked on arbitrary threads, so handlers must marshal to the UI thread
	/// themselves.
	/// </param>
	/// <param name="cancellationToken">A token that abandons in-flight requests.</param>
	/// <returns>
	/// The resolved resources in the order the API listed their URLs, regardless of the order the individual
	/// responses arrived in.
	/// </returns>
	/// <remarks>
	/// One request per URL is unavoidable — the API exposes no batch endpoint. Concurrency is bounded and
	/// results are cached per URL, so revisiting a film costs nothing.
	/// </remarks>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
	Task<IReadOnlyList<RelatedResource>> GetRelatedResourcesAsync(
		Film film,
		RelatedResourceKind kind,
		IProgress<RelatedResource>? progress = null,
		CancellationToken cancellationToken = default);
}
