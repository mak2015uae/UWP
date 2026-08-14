using System;

namespace DrawboardCodingExercise.Services.Api;

/// <summary>
/// Builds the absolute request URI for an API call, accepting either a path relative to the configured base
/// address or an absolute URL taken from a payload.
/// </summary>
/// <remarks>
/// Payloads link related resources by absolute URL while calling code naturally writes short relative paths, so
/// the client has to accept both. <see cref="Uri"/>'s own base-relative constructor already resolves both forms
/// correctly — string concatenation does not, and would turn an absolute URL into
/// <c>{base}/https://host/path</c>, a request that fails looking like a server fault.
/// <para>
/// Resolution alone is not enough, though: <see cref="Uri"/> happily accepts an absolute URL pointing anywhere
/// at all, which would let a payload send the client to a host of its choosing. Every resolved URI is therefore
/// checked to be beneath the base address, and rejected loudly if it is not.
/// </para>
/// </remarks>
public static class RequestUriResolver
{
	/// <summary>
	/// Normalizes a configured base address into a URI that paths can be resolved against.
	/// </summary>
	/// <param name="serverAddress">The configured base address, with or without a trailing slash.</param>
	/// <returns>The base address as an absolute URI, always ending in a slash.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="serverAddress"/> is null, empty, or not an absolute URL.
	/// </exception>
	/// <remarks>
	/// The trailing slash is what makes the address a directory rather than a file. Without it, resolving
	/// <c>films</c> against <c>https://host/api</c> yields <c>https://host/films</c> — the last segment of the
	/// base is treated as a file name and silently replaced.
	/// </remarks>
	public static Uri CreateBaseUri(string serverAddress)
	{
		if (string.IsNullOrWhiteSpace(serverAddress))
		{
			throw new ArgumentException("A base address is required.", nameof(serverAddress));
		}

		var normalized = serverAddress.Trim();

		if (!normalized.EndsWith("/", StringComparison.Ordinal))
		{
			normalized += "/";
		}

		if (!Uri.TryCreate(normalized, UriKind.Absolute, out var baseUri))
		{
			throw new ArgumentException($"'{serverAddress}' is not an absolute URL.", nameof(serverAddress));
		}

		return baseUri;
	}

	/// <summary>
	/// Resolves a request path against a base address.
	/// </summary>
	/// <param name="baseUri">The base address, as produced by <see cref="CreateBaseUri"/>.</param>
	/// <param name="path">
	/// Either a path relative to <paramref name="baseUri"/> — a leading slash is tolerated and ignored rather
	/// than escaping to the host root — or an absolute URL beneath it, as payloads supply.
	/// </param>
	/// <returns>The absolute URI to request.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="baseUri"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="path"/> is null or empty, or resolves outside <paramref name="baseUri"/> — a different
	/// scheme, host or port, or a path above the base path. Such a resource cannot be served by this client and
	/// needs one configured for that origin.
	/// </exception>
	public static Uri Resolve(Uri baseUri, string path)
	{
		if (baseUri is null)
		{
			throw new ArgumentNullException(nameof(baseUri));
		}

		if (string.IsNullOrWhiteSpace(path))
		{
			throw new ArgumentException("A request path is required.", nameof(path));
		}

		var trimmed = path.Trim();

		if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var resolved))
		{
			// A relative path. Leading slashes would resolve to the host root and drop the base path, so they
			// are stripped — the scaffold's callers have always been allowed to write "/films".
			resolved = new Uri(baseUri, trimmed.TrimStart('/'));
		}

		if (!IsBeneath(baseUri, resolved))
		{
			throw new ArgumentException(
				$"'{path}' resolves to '{resolved}', which is outside the API base address '{baseUri}'. " +
				"This client cannot reach it; a client configured for that origin is required.",
				nameof(path));
		}

		return resolved;
	}

	/// <summary>
	/// Determines whether a resolved URI sits beneath a base address.
	/// </summary>
	/// <param name="baseUri">The base address.</param>
	/// <param name="candidate">The resolved URI.</param>
	/// <returns>
	/// <see langword="true"/> when both share an origin and the candidate's path starts with the base path.
	/// </returns>
	private static bool IsBeneath(Uri baseUri, Uri candidate)
	{
		var sameOrigin = Uri.Compare(
			baseUri,
			candidate,
			UriComponents.SchemeAndServer,
			UriFormat.SafeUnescaped,
			StringComparison.OrdinalIgnoreCase) == 0;

		return sameOrigin &&
		       candidate.AbsolutePath.StartsWith(baseUri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
	}
}
