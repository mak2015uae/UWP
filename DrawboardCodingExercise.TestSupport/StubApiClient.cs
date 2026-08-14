using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Services;
using DrawboardCodingExercise.Services.Api;
using Newtonsoft.Json;

namespace DrawboardCodingExercise.TestSupport;

/// <summary>
/// An in-memory <see cref="IAPIClient"/> that serves canned JSON, records what was requested, and can be made
/// to fail or to respond slowly.
/// </summary>
/// <remarks>
/// Deserializes with the production serializer settings, so a test that asserts on mapped values also proves
/// the data transfer objects are annotated correctly — a substituted client returning ready-made objects would
/// silently pass even if every property name were wrong.
/// <para>
/// All members are safe to use from several threads at once, because the code under test issues concurrent
/// requests deliberately.
/// </para>
/// </remarks>
public sealed class StubApiClient : IAPIClient
{
	private readonly JsonSerializerSettings _serializerSettings = ApiSerializerSettings.Create();
	private readonly Uri _baseUri = RequestUriResolver.CreateBaseUri(StubApiSettings.DefaultServerAddress);
	private readonly object _gate = new();
	private readonly List<string> _requestedPaths = new();

	private int _inFlight;

	/// <summary>
	/// Gets the canned response bodies, keyed by request path.
	/// </summary>
	/// <value>
	/// A path with no entry causes the request to fail with <see cref="HttpStatusException"/> carrying
	/// <c>404</c>, mirroring how a real server treats an unknown resource.
	/// </value>
	/// <remarks>
	/// Keys and incoming requests are matched on their resolved absolute URL, so a test may register either
	/// <c>people/1</c> or the full <c>https://swapi.info/api/people/1</c> and match a caller using the other
	/// form — which is exactly the flexibility the real client offers.
	/// </remarks>
	public Dictionary<string, string> ResponsesByPath { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets the exceptions to throw instead of responding, keyed by request path.
	/// </summary>
	/// <remarks>Takes precedence over <see cref="ResponsesByPath"/>, so a path can be made to fail on demand.</remarks>
	public Dictionary<string, Exception> FailuresByPath { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets or sets an artificial delay applied to every request.
	/// </summary>
	/// <value>
	/// Zero by default. A small non-zero delay forces concurrent requests to genuinely overlap, which is what
	/// makes <see cref="MaxConcurrentRequests"/> meaningful.
	/// </value>
	public TimeSpan ResponseDelay { get; set; } = TimeSpan.Zero;

	/// <summary>
	/// Gets the highest number of requests that were in flight simultaneously.
	/// </summary>
	public int MaxConcurrentRequests { get; private set; }

	/// <summary>
	/// Gets every path that has been requested, in the order the requests started.
	/// </summary>
	/// <returns>A snapshot copy, safe to enumerate while further requests are running.</returns>
	public IReadOnlyList<string> RequestedPaths()
	{
		lock (_gate)
		{
			return new List<string>(_requestedPaths);
		}
	}

	/// <summary>
	/// Counts how many times a path has been requested.
	/// </summary>
	/// <param name="path">The path to count.</param>
	/// <returns>The number of requests issued for <paramref name="path"/>.</returns>
	public int RequestCount(string path)
	{
		var target = ResolvedKey(path);

		lock (_gate)
		{
			var count = 0;
			foreach (var requested in _requestedPaths)
			{
				if (string.Equals(ResolvedKey(requested), target, StringComparison.OrdinalIgnoreCase))
				{
					count++;
				}
			}

			return count;
		}
	}

	/// <summary>
	/// Normalizes a path or absolute URL into the single form used for matching.
	/// </summary>
	/// <param name="path">The path or absolute URL to normalize.</param>
	/// <returns>
	/// The resolved absolute URL, or the original string when it cannot be resolved — an unresolvable value
	/// still needs to match itself so a test can register a deliberately bad URL.
	/// </returns>
	private string ResolvedKey(string path)
	{
		try
		{
			return RequestUriResolver.Resolve(_baseUri, path).AbsoluteUri;
		}
		catch (ArgumentException)
		{
			return path;
		}
	}

	/// <summary>
	/// Finds the entry registered for a path, matching on the resolved absolute URL.
	/// </summary>
	/// <typeparam name="TValue">The type of value stored against each path.</typeparam>
	/// <param name="entries">The registered entries.</param>
	/// <param name="path">The requested path.</param>
	/// <param name="value">The matching value, when one is registered.</param>
	/// <returns><see langword="true"/> when an entry matched.</returns>
	private bool TryMatch<TValue>(Dictionary<string, TValue> entries, string path, out TValue value)
	{
		var target = ResolvedKey(path);

		foreach (var entry in entries)
		{
			if (string.Equals(ResolvedKey(entry.Key), target, StringComparison.OrdinalIgnoreCase))
			{
				value = entry.Value;
				return true;
			}
		}

		value = default!;
		return false;
	}

	/// <inheritdoc />
	public async Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken = default)
	{
		var body = await RecordAndRespondAsync(path, cancellationToken).ConfigureAwait(false);

		var result = JsonConvert.DeserializeObject<TResponse>(body, _serializerSettings);

		return result ?? throw new JsonSerializationException($"The canned body for '{path}' deserialized to null.");
	}

	/// <inheritdoc />
	public Task<TResponse> PostAsync<TRequest, TResponse>(
		string path,
		TRequest request,
		CancellationToken cancellationToken = default) =>
		GetAsync<TResponse>(path, cancellationToken);

	/// <inheritdoc />
	public async Task<Stream> GetImageAsync(string path, CancellationToken cancellationToken = default)
	{
		var body = await RecordAndRespondAsync(path, cancellationToken).ConfigureAwait(false);

		return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
	}

	/// <summary>
	/// Records a request, applies the configured delay and failure, and returns the canned body.
	/// </summary>
	/// <param name="path">The requested path.</param>
	/// <param name="cancellationToken">The token supplied by the caller, honoured during the delay.</param>
	/// <returns>The canned response body.</returns>
	/// <exception cref="HttpStatusException">
	/// No body is configured for <paramref name="path"/>, or a status failure was configured for it.
	/// </exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
	private async Task<string> RecordAndRespondAsync(string path, CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			_requestedPaths.Add(path);
			_inFlight++;
			if (_inFlight > MaxConcurrentRequests)
			{
				MaxConcurrentRequests = _inFlight;
			}
		}

		try
		{
			if (ResponseDelay > TimeSpan.Zero)
			{
				await Task.Delay(ResponseDelay, cancellationToken).ConfigureAwait(false);
			}

			cancellationToken.ThrowIfCancellationRequested();

			if (TryMatch(FailuresByPath, path, out var failure))
			{
				throw failure;
			}

			if (!TryMatch(ResponsesByPath, path, out var body))
			{
				throw new HttpStatusException(System.Net.HttpStatusCode.NotFound);
			}

			return body;
		}
		finally
		{
			lock (_gate)
			{
				_inFlight--;
			}
		}
	}
}
