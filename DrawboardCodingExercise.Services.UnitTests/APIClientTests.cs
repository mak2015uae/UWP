using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Services.Api;
using DrawboardCodingExercise.TestSupport;
using Newtonsoft.Json;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests;

/// <summary>
/// Tests for <see cref="APIClient"/>, driven through an in-memory message handler so the request it builds and
/// the way it interprets a response can be asserted without a network.
/// </summary>
/// <remarks>
/// Previously this class had no offline coverage at all: its shared client is static, so the only way to exercise
/// it was against the live service. The handler seam closes that gap, which matters because the URI it resolves
/// and the status codes it translates are exactly what the rest of the application's error handling is built on.
/// </remarks>
public class APIClientTests
{
	private readonly RecordingHttpMessageHandler _handler = new();

	/// <summary>
	/// Creates a client sending through the recording handler.
	/// </summary>
	/// <param name="serverAddress">The base address to configure.</param>
	/// <returns>The client under test.</returns>
	private APIClient CreateClient(string serverAddress = StubApiSettings.DefaultServerAddress) =>
		new(
			new StubApiSettings(serverAddress),
			ApiSerializerSettings.Create(),
			SilentLogger.Create(),
			_handler);

	/// <summary>
	/// A relative path is resolved beneath the configured base address.
	/// </summary>
	[Fact]
	public async Task GetAsync_RelativePath_RequestsTheResolvedUri()
	{
		_handler.ResponseBody = "[]";

		await CreateClient().GetAsync<object[]>("films");

		_handler.SingleRequest().RequestUri!.AbsoluteUri.ShouldBe("https://swapi.info/api/films");
	}

	/// <summary>
	/// An absolute URL from a payload is requested as-is. This is the regression test for the defect that made
	/// the client request <c>{base}/https://host/path</c> and fail looking like a server fault.
	/// </summary>
	[Fact]
	public async Task GetAsync_AbsoluteUrlFromAPayload_RequestsItUnchanged()
	{
		await CreateClient().GetAsync<object>("https://swapi.info/api/people/1");

		var requested = _handler.SingleRequest().RequestUri!.AbsoluteUri;
		requested.ShouldBe("https://swapi.info/api/people/1");
		requested.ShouldNotContain("api/https", Case.Sensitive);
	}

	/// <summary>
	/// A leading slash does not escape the base path.
	/// </summary>
	[Fact]
	public async Task GetAsync_LeadingSlash_StaysBeneathTheBasePath()
	{
		_handler.ResponseBody = "[]";

		await CreateClient().GetAsync<object[]>("/films");

		_handler.SingleRequest().RequestUri!.AbsoluteUri.ShouldBe("https://swapi.info/api/films");
	}

	/// <summary>
	/// A URL on another origin is rejected before any request is attempted, so a payload cannot redirect the
	/// client to a host of its choosing.
	/// </summary>
	[Fact]
	public async Task GetAsync_ForeignOrigin_ThrowsWithoutSendingAnything()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			CreateClient().GetAsync<object>("https://images.metmuseum.org/CRDImages/x.jpg"));

		_handler.Requests.ShouldBeEmpty("no request should leave the process");
	}

	/// <summary>
	/// Every request carries the correlation identifier, so a client log line can be matched in server logs.
	/// </summary>
	[Fact]
	public async Task GetAsync_SendsACorrelationIdentifier()
	{
		_handler.ResponseBody = "[]";

		await CreateClient().GetAsync<object[]>("films");

		var headers = _handler.SingleRequest().Headers;
		headers.Keys.ShouldContain("X-Correlation-Id");
		headers["X-Correlation-Id"].ShouldNotBeNullOrWhiteSpace();
	}

	/// <summary>
	/// A bare JSON array deserializes, which is the shape this API returns for its collections.
	/// </summary>
	[Fact]
	public async Task GetAsync_BareJsonArray_Deserializes()
	{
		_handler.ResponseBody = @"[{""name"":""Luke""},{""name"":""Leia""}]";

		var people = await CreateClient().GetAsync<NamedThing[]>("people");

		people.Select(person => person.Name).ShouldBe(new[] { "Luke", "Leia" });
	}

	/// <summary>
	/// A non-success status becomes the exception the application's retry policy is built around, carrying the
	/// code so the user can be told which kind of failure it was.
	/// </summary>
	/// <param name="statusCode">The status code the server returns.</param>
	[Theory]
	[InlineData(HttpStatusCode.NotFound)]
	[InlineData(HttpStatusCode.InternalServerError)]
	[InlineData(HttpStatusCode.BadGateway)]
	[InlineData(HttpStatusCode.Forbidden)]
	public async Task GetAsync_NonSuccessStatus_ThrowsCarryingTheStatusCode(HttpStatusCode statusCode)
	{
		_handler.StatusCode = statusCode;

		var failure = await Should.ThrowAsync<HttpStatusException>(() =>
			CreateClient().GetAsync<object>("films"));

		failure.StatusCode.ShouldBe(statusCode);
	}

	/// <summary>
	/// A body that deserializes to nothing fails loudly rather than handing back a null the signature forbids —
	/// which would otherwise surface as a confusing null reference much further away.
	/// </summary>
	/// <param name="body">The unusable response body.</param>
	[Theory]
	[InlineData("")]
	[InlineData("null")]
	public async Task GetAsync_EmptyOrNullBody_ThrowsRatherThanReturningNull(string body)
	{
		_handler.ResponseBody = body;

		var failure = await Should.ThrowAsync<JsonSerializationException>(() =>
			CreateClient().GetAsync<NamedThing>("people/1"));

		failure.Message.ShouldContain("people/1");
	}

	/// <summary>
	/// A transport failure propagates untranslated, so the retry policy can distinguish being offline from a
	/// server that answered badly.
	/// </summary>
	[Fact]
	public async Task GetAsync_TransportFailure_PropagatesAsHttpRequestException()
	{
		_handler.TransportFailure = new HttpRequestException("no network");

		await Should.ThrowAsync<HttpRequestException>(() => CreateClient().GetAsync<object>("films"));
	}

	/// <summary>
	/// The cancellation token reaches the transport, so navigating away abandons an in-flight request.
	/// </summary>
	[Fact]
	public async Task GetAsync_CancelledToken_AbandonsTheRequest()
	{
		_handler.ResponseDelay = TimeSpan.FromSeconds(5);
		using var cancellation = new CancellationTokenSource();
		var request = CreateClient().GetAsync<object>("films", cancellation.Token);

		cancellation.Cancel();

		await Should.ThrowAsync<OperationCanceledException>(() => request);
	}

	/// <summary>
	/// A posted body is serialized as JSON and declared as such.
	/// </summary>
	[Fact]
	public async Task PostAsync_SendsTheBodyAsJson()
	{
		_handler.ResponseBody = @"{""name"":""created""}";

		await CreateClient().PostAsync<NamedThing, NamedThing>("people", new NamedThing { Name = "Luke" });

		var request = _handler.SingleRequest();
		request.Method.ShouldBe(HttpMethod.Post);
		request.ContentType.ShouldBe("application/json");
		request.Body.ShouldNotBeNull();
		request.Body!.ShouldContain("Luke");
	}

	/// <summary>
	/// A binary resource comes back as a readable stream.
	/// </summary>
	[Fact]
	public async Task GetImageAsync_ReturnsTheResponseAsAStream()
	{
		_handler.ResponseBody = "not really an image";

		using var stream = await CreateClient().GetImageAsync("images/1.png");
		using var reader = new System.IO.StreamReader(stream);

		(await reader.ReadToEndAsync()).ShouldBe("not really an image");
	}

	/// <summary>
	/// A misconfigured base address is reported when the client is built, so it cannot be mistaken later for the
	/// server being unreachable.
	/// </summary>
	[Fact]
	public void Constructor_BaseAddressNotAbsolute_Throws()
	{
		Should.Throw<ArgumentException>(() => CreateClient("not a url"));
	}

	/// <summary>
	/// A base address without a trailing slash still resolves beneath its path rather than losing the last
	/// segment.
	/// </summary>
	[Fact]
	public async Task GetAsync_BaseAddressWithoutTrailingSlash_KeepsThePathPrefix()
	{
		_handler.ResponseBody = "[]";

		await CreateClient("https://swapi.info/api").GetAsync<object[]>("films");

		_handler.SingleRequest().RequestUri!.AbsoluteUri.ShouldBe("https://swapi.info/api/films");
	}

	/// <summary>
	/// A minimal payload shape for deserialization assertions.
	/// </summary>
	public sealed class NamedThing
	{
		/// <summary>Gets or sets the name.</summary>
		public string? Name { get; set; }
	}
}
