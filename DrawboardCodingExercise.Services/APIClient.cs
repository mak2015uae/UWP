using Newtonsoft.Json;
using Serilog.Context;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.CoreFramework;
using DrawboardCodingExercise.Services.Api;
using Serilog;

namespace DrawboardCodingExercise.Services;

/// <summary>
/// The default <see cref="IAPIClient"/>, backed by a shared <see cref="HttpClient"/> and Newtonsoft.Json.
/// </summary>
/// <remarks>
/// Every call is wrapped in an <see cref="ActionContext"/> so that a single logical operation — including the
/// fan-out of related-resource requests — shares one correlation identifier, which is also sent to the server
/// as <c>X-Correlation-Id</c> and can be matched in server logs.
/// </remarks>
public class APIClient : IAPIClient
{
	private readonly Uri _baseUri;
	private readonly JsonSerializerSettings _jsonSerializerSettings;
	private readonly ILogger _logger;
	private readonly HttpClient _client;

	//Initialize the HttpClient statically, as per the Microsoft docs:
	//  HttpClient is intended to be instantiated once and reused throughout the life of an application.
	//  The following conditions can result in SocketException errors:
	//    * Creating a new HttpClient instance per request
	//    * Server under heavy load.
	//
	// see: https://docs.microsoft.com/en-us/aspnet/web-api/overview/advanced/calling-a-web-api-from-a-net-client
	private static readonly HttpClient SharedClient = CreateClient(null);

	/// <summary>
	/// Creates a client with the request headers every call shares.
	/// </summary>
	/// <param name="messageHandler">
	/// The handler to send through, or <see langword="null"/> for the platform default.
	/// </param>
	/// <returns>The configured client.</returns>
	private static HttpClient CreateClient(HttpMessageHandler? messageHandler)
	{
		var client = messageHandler is null ? new HttpClient() : new HttpClient(messageHandler);

		client.DefaultRequestHeaders.Clear();
		client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		client.DefaultRequestHeaders.Remove("Authorization");

		return client;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="APIClient"/> class.
	/// </summary>
	/// <param name="apiSettings">Supplies the base address every request path is resolved against.</param>
	/// <param name="jsonSerializerSettings">
	/// The serializer settings applied to every request and response body.
	/// </param>
	/// <param name="logger">Receives per-request diagnostics, enriched with the correlation identifier.</param>
	/// <exception cref="ArgumentException">
	/// <see cref="IAPISettings.ServerAddress"/> is not an absolute URL. Failing here rather than on the first
	/// request turns a misconfiguration into an immediate, obvious error instead of one that looks like the
	/// server being unreachable.
	/// </exception>
	public APIClient(
		IAPISettings apiSettings,
		JsonSerializerSettings jsonSerializerSettings,
		ILogger logger)
		: this(apiSettings, jsonSerializerSettings, logger, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="APIClient"/> class, sending through a supplied handler.
	/// </summary>
	/// <param name="apiSettings">Supplies the base address every request path is resolved against.</param>
	/// <param name="jsonSerializerSettings">
	/// The serializer settings applied to every request and response body.
	/// </param>
	/// <param name="logger">Receives per-request diagnostics, enriched with the correlation identifier.</param>
	/// <param name="messageHandler">
	/// The handler to send through, or <see langword="null"/> to use the application-wide shared client. Supplying
	/// one gives this class a seam with no network: a test can assert on the exact request that would have gone
	/// out, and a <see cref="DelegatingHandler"/> can add cross-cutting behaviour such as retry or telemetry.
	/// </param>
	/// <exception cref="ArgumentException">
	/// <see cref="IAPISettings.ServerAddress"/> is not an absolute URL.
	/// </exception>
	/// <remarks>
	/// A supplied handler gets its own client, so it never disturbs the shared one — which stays a single instance
	/// per application, as the platform guidance requires.
	/// </remarks>
	public APIClient(
		IAPISettings apiSettings,
		JsonSerializerSettings jsonSerializerSettings,
		ILogger logger,
		HttpMessageHandler? messageHandler)
	{
		_baseUri = RequestUriResolver.CreateBaseUri(apiSettings.ServerAddress);
		_jsonSerializerSettings = jsonSerializerSettings;
		_logger = logger;
		_client = messageHandler is null ? SharedClient : CreateClient(messageHandler);
	}

	/// <inheritdoc />
	/// <exception cref="JsonSerializationException">
	/// The response body was empty or deserialized to <see langword="null"/>, which the non-nullable return
	/// type cannot represent.
	/// </exception>
	public async Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken = default)
	{
		using (LogContext.PushProperty("ResponseType", typeof(TResponse).Name))
		{
			var response = await CallService(
				requestUri => new HttpRequestMessage(HttpMethod.Get, requestUri),
				path,
				cancellationToken);

			var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
			return Deserialize<TResponse>(responseContent, path);
		}
	}

	/// <inheritdoc />
	/// <exception cref="JsonSerializationException">
	/// The response body was empty or deserialized to <see langword="null"/>, which the non-nullable return
	/// type cannot represent.
	/// </exception>
	public async Task<TResponse> PostAsync<TRequest, TResponse>(
		string path,
		TRequest request,
		CancellationToken cancellationToken = default)
	{
		using (LogContext.PushProperty("ResponseType", typeof(TResponse).Name))
		{
			var response = await CallService(
				requestUri =>
				{
					var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);
					var content = new StringContent(
						JsonConvert.SerializeObject(request, _jsonSerializerSettings),
						Encoding.UTF8,
						"application/json"
					);
					httpRequestMessage.Content = content;
					return httpRequestMessage;
				},
				path,
				cancellationToken);

			var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
			return Deserialize<TResponse>(responseContent, path);
		}
	}

	/// <inheritdoc />
	public async Task<Stream> GetImageAsync(string path, CancellationToken cancellationToken = default)
	{
		using (LogContext.PushProperty("ResponseType", "Image"))
		{
			var response = await CallService(
				requestUri => new HttpRequestMessage(HttpMethod.Get, requestUri),
				path,
				cancellationToken);

			return await response.ReadAsStreamAsync();
		}
	}

	/// <summary>
	/// Deserializes a response body, failing loudly rather than handing back a null the signature forbids.
	/// </summary>
	/// <typeparam name="TResponse">The type to deserialize into.</typeparam>
	/// <param name="responseContent">The raw response body.</param>
	/// <param name="path">The request path, included in the failure message for diagnosis.</param>
	/// <returns>The deserialized body.</returns>
	/// <exception cref="JsonSerializationException">
	/// The body was empty, or was the literal <c>null</c>.
	/// </exception>
	private TResponse Deserialize<TResponse>(string responseContent, string path)
	{
		var result = JsonConvert.DeserializeObject<TResponse>(responseContent, _jsonSerializerSettings);

		if (result is null)
		{
			throw new JsonSerializationException(
				$"The response body for '{path}' was empty or null and cannot be represented as " +
				$"{typeof(TResponse).Name}.");
		}

		return result;
	}

	/// <summary>
	/// Resolves the request URI, sends the request, logs its outcome, and translates a non-success status into
	/// an exception.
	/// </summary>
	/// <param name="requestBuilder">
	/// Builds the request from the resolved URI, so each caller controls only method and body.
	/// </param>
	/// <param name="path">
	/// The caller's path, relative to the base address or absolute beneath it.
	/// </param>
	/// <param name="cancellationToken">A token that abandons the request.</param>
	/// <returns>The successful response's content.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="path"/> is empty, or resolves outside the configured base address.
	/// </exception>
	/// <exception cref="HttpStatusException">The response status was outside the 2xx range.</exception>
	/// <exception cref="HttpRequestException">The request failed at the transport level.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
	private async Task<HttpContent> CallService(
		Func<Uri, HttpRequestMessage> requestBuilder,
		string path,
		CancellationToken cancellationToken)
	{
		var request = requestBuilder(RequestUriResolver.Resolve(_baseUri, path));

		using (ActionContext.PushActionContext())
		using (LogContext.PushProperty("RequestMethod", request.Method))
		using (LogContext.PushProperty("RequestPath", request.RequestUri))
		{
			var sw = Stopwatch.StartNew();

			request.Headers.Add("X-Correlation-Id", ActionContext.CorrelationId);

			_logger.Verbose("REST {RequestMethod} {RequestPath} called");
			var response = await _client
				.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
				.ConfigureAwait(false);

			sw.Stop();

			using (LogContext.PushProperty("StatusCode", response.StatusCode))
			using (LogContext.PushProperty("Elapsed", sw.Elapsed.TotalMilliseconds))
			{
				switch ((int?) response.StatusCode)
				{
					case var code when code >= 200 && code < 300:
						_logger.Debug("REST {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.000} ms");
						break;
					case var code when code >= 300 && code < 500:
						_logger.Warning("REST {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.000} ms");
						break;
					default:
						_logger.Error("REST {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.000} ms");
						break;
				}
			}

			// Where there is no status specific exception throw a catch-all exception containing the status code.
			if (!response.IsSuccessStatusCode)
			{
				throw new HttpStatusException(response.StatusCode);
			}

			return response.Content;
		}
	}
}
