using System;
using DrawboardCodingExercise.Services.Api;
using Shouldly;
using Xunit;

namespace DrawboardCodingExercise.Services.UnitTests;

/// <summary>
/// Tests for <see cref="RequestUriResolver"/>, which builds every request URI the API client sends.
/// </summary>
/// <remarks>
/// This is the seam that decides whether a payload's absolute resource URL becomes a valid request or a
/// malformed one, so its edge cases are worth more attention than their size suggests.
/// </remarks>
public class RequestUriResolverTests
{
	private const string BaseAddress = "https://swapi.info/api/";

	private static Uri BaseUri => RequestUriResolver.CreateBaseUri(BaseAddress);

	/// <summary>
	/// The ordinary case: a short relative path is resolved beneath the base address.
	/// </summary>
	[Fact]
	public void Resolve_RelativePath_ResolvesBeneathTheBaseAddress()
	{
		RequestUriResolver.Resolve(BaseUri, "films")
			.AbsoluteUri.ShouldBe("https://swapi.info/api/films");
	}

	/// <summary>
	/// An absolute URL from a payload is used as-is rather than appended to the base address. Appending is what
	/// produced <c>{base}/https://host/path</c>, a request that fails looking like a server fault.
	/// </summary>
	[Fact]
	public void Resolve_AbsoluteUrlBeneathTheBase_IsUsedUnchanged()
	{
		RequestUriResolver.Resolve(BaseUri, "https://swapi.info/api/people/1")
			.AbsoluteUri.ShouldBe("https://swapi.info/api/people/1");
	}

	/// <summary>
	/// A leading slash is tolerated on a relative path. Resolved literally it would jump to the host root and
	/// silently drop the base path.
	/// </summary>
	[Fact]
	public void Resolve_RelativePathWithLeadingSlash_StaysBeneathTheBasePath()
	{
		RequestUriResolver.Resolve(BaseUri, "/films")
			.AbsoluteUri.ShouldBe("https://swapi.info/api/films");
	}

	/// <summary>
	/// A base address without a trailing slash must not lose its last segment. Treated as a file rather than a
	/// directory, <c>https://swapi.info/api</c> would resolve <c>films</c> to <c>https://swapi.info/films</c>.
	/// </summary>
	[Fact]
	public void CreateBaseUri_WithoutTrailingSlash_StillResolvesBeneathTheBasePath()
	{
		var baseUri = RequestUriResolver.CreateBaseUri("https://swapi.info/api");

		RequestUriResolver.Resolve(baseUri, "films")
			.AbsoluteUri.ShouldBe("https://swapi.info/api/films");
	}

	/// <summary>
	/// Trailing slashes on the resource itself are part of the path the server published, so they survive.
	/// </summary>
	[Fact]
	public void Resolve_ResourceWithTrailingSlash_KeepsIt()
	{
		RequestUriResolver.Resolve(BaseUri, "https://swapi.info/api/films/9/")
			.AbsoluteUri.ShouldBe("https://swapi.info/api/films/9/");
	}

	/// <summary>
	/// Query strings are preserved, since they select what the server returns.
	/// </summary>
	[Fact]
	public void Resolve_PathWithQuery_PreservesTheQuery()
	{
		RequestUriResolver.Resolve(BaseUri, "people?page=2")
			.AbsoluteUri.ShouldBe("https://swapi.info/api/people?page=2");
	}

	/// <summary>
	/// Host comparison ignores case, as hostnames are case-insensitive.
	/// </summary>
	[Fact]
	public void Resolve_HostDifferingOnlyByCase_IsAccepted()
	{
		RequestUriResolver.Resolve(BaseUri, "https://SWAPI.info/api/people/1")
			.AbsolutePath.ShouldBe("/api/people/1");
	}

	/// <summary>
	/// A resource on another origin is rejected rather than requested. Resolution alone would happily accept it,
	/// which would let a payload send this client anywhere it liked.
	/// </summary>
	[Fact]
	public void Resolve_ForeignHost_Throws()
	{
		var failure = Should.Throw<ArgumentException>(() =>
			RequestUriResolver.Resolve(BaseUri, "https://images.metmuseum.org/CRDImages/x.jpg"));

		failure.Message.ShouldContain("outside the API base address");
	}

	/// <summary>
	/// A different scheme is a different origin, even on the same host.
	/// </summary>
	[Fact]
	public void Resolve_DifferentScheme_Throws()
	{
		Should.Throw<ArgumentException>(() =>
			RequestUriResolver.Resolve(BaseUri, "http://swapi.info/api/people/1"));
	}

	/// <summary>
	/// A resource above the base path is outside what this client serves.
	/// </summary>
	[Fact]
	public void Resolve_PathOutsideTheBasePath_Throws()
	{
		Should.Throw<ArgumentException>(() =>
			RequestUriResolver.Resolve(BaseUri, "https://swapi.info/documentation"));
	}

	/// <summary>
	/// A path that tries to climb above the base with traversal segments is rejected too.
	/// </summary>
	[Fact]
	public void Resolve_RelativeTraversalAboveTheBase_Throws()
	{
		Should.Throw<ArgumentException>(() => RequestUriResolver.Resolve(BaseUri, "../../etc/passwd"));
	}

	/// <summary>
	/// Missing arguments are rejected by name, so the failure points at the caller's mistake.
	/// </summary>
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Resolve_EmptyPath_ThrowsNamingTheArgument(string path)
	{
		Should.Throw<ArgumentException>(() => RequestUriResolver.Resolve(BaseUri, path))
			.ParamName.ShouldBe("path");
	}

	/// <summary>
	/// A missing base URI is a programming error rather than bad input.
	/// </summary>
	[Fact]
	public void Resolve_NullBaseUri_Throws()
	{
		Should.Throw<ArgumentNullException>(() => RequestUriResolver.Resolve(null!, "films"));
	}

	/// <summary>
	/// A misconfigured base address is reported when the client is constructed, not on the first request, so it
	/// cannot be mistaken for the server being unreachable.
	/// </summary>
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("not a url")]
	[InlineData("/api/")]
	public void CreateBaseUri_NotAnAbsoluteUrl_ThrowsNamingTheArgument(string serverAddress)
	{
		Should.Throw<ArgumentException>(() => RequestUriResolver.CreateBaseUri(serverAddress))
			.ParamName.ShouldBe("serverAddress");
	}
}
