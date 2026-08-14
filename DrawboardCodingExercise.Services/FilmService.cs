using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.Model;
using DrawboardCodingExercise.Contracts.Services;
using DrawboardCodingExercise.Services.Api.Dto;
using DrawboardCodingExercise.Services.Mapping;
using Serilog;

namespace DrawboardCodingExercise.Services;

/// <summary>
/// The default <see cref="IFilmService"/>: retrieves films over <see cref="IAPIClient"/>, maps them to the
/// domain model, and caches the results for the lifetime of the application.
/// </summary>
/// <remarks>
/// Registered as a single instance. The cache is what makes the detail page cheap: the films endpoint returns
/// every field the detail page shows, including the opening crawl, so once the list has been retrieved a
/// detail view costs no network call at all. It also compensates for page ViewModels being rebuilt on every
/// navigation — without it, each back navigation would re-fetch.
/// </remarks>
public sealed class FilmService : IFilmService
{
	/// <summary>
	/// The number of related-resource requests allowed in flight at once.
	/// </summary>
	/// <remarks>
	/// The API exposes no batch endpoint, so a film with eighteen characters needs eighteen requests. Firing
	/// them all at once invites throttling from a free public service and gains little, since the UI fills
	/// incrementally either way.
	/// </remarks>
	private const int MaxConcurrentRelatedRequests = 6;

	/// <summary>The path of the films collection, relative to the configured base address.</summary>
	private const string FilmsPath = "films";

	private readonly IAPIClient _apiClient;
	private readonly ILogger _logger;

	/// <summary>Serializes cache population so concurrent callers share one network call.</summary>
	private readonly SemaphoreSlim _filmCacheGate = new SemaphoreSlim(1, 1);

	/// <summary>Caches resolved related resources by their absolute URL.</summary>
	private readonly ConcurrentDictionary<string, RelatedResource> _relatedResourceCache =
		new ConcurrentDictionary<string, RelatedResource>(StringComparer.OrdinalIgnoreCase);

	/// <summary>The cached film list, or <see langword="null"/> while the cache is cold.</summary>
	private IReadOnlyList<Film>? _films;

	/// <summary>
	/// Initializes a new instance of the <see cref="FilmService"/> class.
	/// </summary>
	/// <param name="apiClient">
	/// Performs the HTTP calls. It resolves both relative paths and the absolute resource URLs that payloads
	/// carry, so this service needs no knowledge of the API's address.
	/// </param>
	/// <param name="logger">Receives cache and fan-out diagnostics.</param>
	public FilmService(IAPIClient apiClient, ILogger logger)
	{
		_apiClient = apiClient;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<Film>> GetFilmsAsync(CancellationToken cancellationToken = default)
	{
		var cached = _films;
		if (cached is not null)
		{
			return cached;
		}

		await _filmCacheGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Re-check inside the gate: a concurrent caller may have populated the cache while we waited.
			if (_films is not null)
			{
				return _films;
			}

			var payload = await _apiClient
				.GetAsync<FilmDto[]>(FilmsPath, cancellationToken)
				.ConfigureAwait(false);

			var films = FilmMapper.ToDomain(payload);
			_films = films;

			_logger.Information("Retrieved and cached {FilmCount} films", films.Count);

			return films;
		}
		finally
		{
			_filmCacheGate.Release();
		}
	}

	/// <inheritdoc />
	public async Task<Film?> GetFilmAsync(int filmId, CancellationToken cancellationToken = default)
	{
		var films = await GetFilmsAsync(cancellationToken).ConfigureAwait(false);

		return films.FirstOrDefault(film => film.Id == filmId);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<RelatedResource>> GetRelatedResourcesAsync(
		Film film,
		RelatedResourceKind kind,
		IProgress<RelatedResource>? progress = null,
		CancellationToken cancellationToken = default)
	{
		if (film is null)
		{
			throw new ArgumentNullException(nameof(film));
		}

		if (!film.RelatedResourceUrls.TryGetValue(kind, out var urls) || urls.Count == 0)
		{
			return Array.Empty<RelatedResource>();
		}

		// Results are placed by index so the returned order matches the API's listing order, regardless of
		// the order the individual responses happen to arrive in.
		var resolved = new RelatedResource?[urls.Count];

		using (var throttle = new SemaphoreSlim(MaxConcurrentRelatedRequests, MaxConcurrentRelatedRequests))
		{
			var fetches = urls
				.Select((url, index) => ResolveAsync(url, index, kind, resolved, throttle, progress, cancellationToken))
				.ToArray();

			await Task.WhenAll(fetches).ConfigureAwait(false);
		}

		// Unusable URLs leave their slot empty rather than failing the batch, so one malformed entry costs the
		// user that single row instead of the entire list.
		return resolved.Where(resource => resource is not null).Select(resource => resource!).ToList();
	}

	/// <summary>
	/// Resolves one related resource into its slot, respecting the concurrency limit and the cache.
	/// </summary>
	/// <param name="url">The absolute resource URL to resolve.</param>
	/// <param name="index">The slot in <paramref name="resolved"/> this result belongs in.</param>
	/// <param name="kind">The category being resolved, recorded on the result.</param>
	/// <param name="resolved">
	/// The pre-sized result array. Each task writes only its own index, and leaves it empty if the URL cannot be
	/// requested.
	/// </param>
	/// <param name="throttle">Bounds the number of concurrent requests.</param>
	/// <param name="progress">Optional sink notified as soon as this resource is known.</param>
	/// <param name="cancellationToken">A token that abandons the request.</param>
	/// <returns>A task that completes when this resource has been resolved and recorded.</returns>
	/// <remarks>
	/// A URL the client cannot reach — malformed, or pointing at another origin — is logged and skipped rather
	/// than thrown, matching the tolerance the mapper already applies to every other field of a public payload.
	/// Request failures are a different matter and still propagate, so the caller can offer a retry.
	/// </remarks>
	private async Task ResolveAsync(
		string url,
		int index,
		RelatedResourceKind kind,
		RelatedResource?[] resolved,
		SemaphoreSlim throttle,
		IProgress<RelatedResource>? progress,
		CancellationToken cancellationToken)
	{
		if (_relatedResourceCache.TryGetValue(url, out var cached))
		{
			resolved[index] = cached;
			progress?.Report(cached);
			return;
		}

		await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var payload = await _apiClient
				.GetAsync<NamedResourceDto>(url, cancellationToken)
				.ConfigureAwait(false);

			var resource = new RelatedResource(payload.Name ?? string.Empty, kind, url);

			_relatedResourceCache.TryAdd(url, resource);
			resolved[index] = resource;
			progress?.Report(resource);
		}
		catch (ArgumentException unusableUrl)
		{
			_logger.Warning(
				unusableUrl,
				"Skipping related {ResourceKind} resource {ResourceUrl}, which this client cannot request",
				kind,
				url);
		}
		finally
		{
			throttle.Release();
		}
	}
}
