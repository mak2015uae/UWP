using System;
using System.Net;

namespace DrawboardCodingExercise.Services;

/// <summary>
/// The exception that is thrown when an HTTP request returns a status code not in the 2xx range.
/// </summary>
/// <remarks>
/// Carries the status code so callers can distinguish a missing resource from throttling or a server fault
/// and report each differently, without depending on message text.
/// </remarks>
public class HttpStatusException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="HttpStatusException"/> class.
	/// </summary>
	/// <param name="statusCode">The status code the server returned.</param>
	public HttpStatusException(HttpStatusCode statusCode)
		: base($"The server responded with status code {(int)statusCode} ({statusCode}).")
	{
		StatusCode = statusCode;
	}

	/// <summary>
	/// Gets the status code the server returned.
	/// </summary>
	public HttpStatusCode StatusCode { get; }
}
