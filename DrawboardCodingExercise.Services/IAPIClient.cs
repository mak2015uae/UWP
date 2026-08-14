using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DrawboardCodingExercise.Services;

/// <summary>
/// An unauthenticated client for calling JSON web APIs relative to a configured base address.
/// </summary>
/// <remarks>
/// Paths are resolved against <see cref="IAPISettings.ServerAddress"/>. Both forms are accepted: a path
/// relative to the base address, and an absolute URL beneath it — which is what payloads use to link related
/// resources, so callers can pass those straight through.
/// <para>
/// A URL resolving outside the base address is rejected with <see cref="ArgumentException"/> rather than
/// requested. This client is configured for one origin; anything else — an image on a separate content host,
/// for instance — needs a client of its own.
/// </para>
/// </remarks>
public interface IAPIClient
{
	/// <summary>
	/// Posts a JSON body and deserializes the JSON response.
	/// </summary>
	/// <typeparam name="TRequest">The request body type, serialized as JSON.</typeparam>
	/// <typeparam name="TResponse">The type the response body is deserialized into.</typeparam>
	/// <param name="path">
	/// The endpoint path, relative to the configured base address, or an absolute URL beneath it.
	/// </param>
	/// <param name="request">The object to send as the request body.</param>
	/// <param name="cancellationToken">A token that abandons the request.</param>
	/// <returns>The deserialized response body.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="path"/> is empty, or resolves outside the configured base address.
	/// </exception>
	/// <exception cref="HttpStatusException">The server responded with a status code outside the 2xx range.</exception>
	/// <exception cref="HttpRequestException">The request failed at the transport level.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
	Task<TResponse> PostAsync<TRequest, TResponse>(
		string path,
		TRequest request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a resource and deserializes the JSON response.
	/// </summary>
	/// <typeparam name="TResponse">
	/// The type the response body is deserialized into. Use an array or collection type for endpoints that
	/// return a bare JSON array rather than an envelope object.
	/// </typeparam>
	/// <param name="path">
	/// The endpoint path, relative to the configured base address, or an absolute URL beneath it — related
	/// resource URLs taken from a payload can be passed unchanged.
	/// </param>
	/// <param name="cancellationToken">A token that abandons the request.</param>
	/// <returns>The deserialized response body.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="path"/> is empty, or resolves outside the configured base address.
	/// </exception>
	/// <exception cref="HttpStatusException">The server responded with a status code outside the 2xx range.</exception>
	/// <exception cref="HttpRequestException">The request failed at the transport level.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
	Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a binary resource as a stream, for example an image.
	/// </summary>
	/// <param name="path">
	/// The resource path, relative to the configured base address, or an absolute URL beneath it.
	/// </param>
	/// <param name="cancellationToken">A token that abandons the request.</param>
	/// <returns>
	/// The response body as a stream. The caller owns the stream and is responsible for disposing it.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="path"/> is empty, or resolves outside the configured base address.
	/// </exception>
	/// <exception cref="HttpStatusException">The server responded with a status code outside the 2xx range.</exception>
	/// <exception cref="HttpRequestException">The request failed at the transport level.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
	/// <remarks>
	/// Bound to the configured origin like every other call, so this cannot fetch an image served from a
	/// different host. An API whose images live on a separate content host needs its own client.
	/// </remarks>
	Task<Stream> GetImageAsync(string path, CancellationToken cancellationToken = default);
}
