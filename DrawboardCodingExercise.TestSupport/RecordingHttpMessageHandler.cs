using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DrawboardCodingExercise.TestSupport;

/// <summary>
/// A snapshot of a request the client attempted, taken while it is still readable.
/// </summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="RequestUri">The fully resolved request URI — the assertion that matters most.</param>
/// <param name="Body">The request body, or <see langword="null"/> when there was none.</param>
/// <param name="ContentType">The body's media type, or <see langword="null"/> when there was no body.</param>
/// <param name="Headers">The request headers, flattened to their first value.</param>
public sealed record RecordedRequest(
	HttpMethod Method,
	Uri? RequestUri,
	string? Body,
	string? ContentType,
	IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// An <see cref="HttpMessageHandler"/> that answers from memory and records what was asked of it.
/// </summary>
/// <remarks>
/// Gives the API client a seam with no network, so the parts worth verifying — the URI it resolved, the headers
/// it attached, how it translates a status code — can be asserted offline instead of only through tests that
/// need a live third-party service.
/// <para>
/// Requests are snapshotted rather than stored, because the client disposes each
/// <see cref="HttpRequestMessage"/> once it has been sent; holding the object would leave assertions reading
/// disposed content.
/// </para>
/// </remarks>
public sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
	private readonly List<RecordedRequest> _requests = new();

	/// <summary>
	/// Gets or sets the status code to answer with.
	/// </summary>
	public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

	/// <summary>
	/// Gets or sets the body to answer with.
	/// </summary>
	public string ResponseBody { get; set; } = "{}";

	/// <summary>
	/// Gets or sets an exception to throw instead of answering, simulating a transport failure.
	/// </summary>
	public Exception? TransportFailure { get; set; }

	/// <summary>
	/// Gets or sets a delay applied before answering, so a cancellation has a window to take effect.
	/// </summary>
	public TimeSpan ResponseDelay { get; set; } = TimeSpan.Zero;

	/// <summary>
	/// Gets the requests attempted, in order.
	/// </summary>
	public IReadOnlyList<RecordedRequest> Requests => _requests;

	/// <summary>
	/// Gets the single request attempted, failing the expectation if there was not exactly one.
	/// </summary>
	/// <returns>The only recorded request.</returns>
	/// <exception cref="InvalidOperationException">There was not exactly one request.</exception>
	public RecordedRequest SingleRequest() => _requests.Count == 1
		? _requests[0]
		: throw new InvalidOperationException($"Expected exactly one request but recorded {_requests.Count}.");

	/// <inheritdoc />
	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		_requests.Add(await SnapshotAsync(request).ConfigureAwait(false));

		if (ResponseDelay > TimeSpan.Zero)
		{
			await Task.Delay(ResponseDelay, cancellationToken).ConfigureAwait(false);
		}

		cancellationToken.ThrowIfCancellationRequested();

		if (TransportFailure is not null)
		{
			throw TransportFailure;
		}

		return new HttpResponseMessage(StatusCode)
		{
			Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
		};
	}

	/// <summary>
	/// Captures everything worth asserting about a request before the client disposes it.
	/// </summary>
	/// <param name="request">The request being sent.</param>
	/// <returns>The snapshot.</returns>
	private static async Task<RecordedRequest> SnapshotAsync(HttpRequestMessage request)
	{
		var body = request.Content is null
			? null
			: await request.Content.ReadAsStringAsync().ConfigureAwait(false);

		var headers = request.Headers
			.ToDictionary(header => header.Key, header => header.Value.FirstOrDefault() ?? string.Empty);

		return new RecordedRequest(
			request.Method,
			request.RequestUri,
			body,
			request.Content?.Headers.ContentType?.MediaType,
			headers);
	}
}
