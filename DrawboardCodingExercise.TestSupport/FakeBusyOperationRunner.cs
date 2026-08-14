using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DrawboardCodingExercise.Contracts.Services;

namespace DrawboardCodingExercise.TestSupport;

/// <summary>
/// An <see cref="IBusyOperationRunner"/> that runs the operation directly, with no progress reporting or retry
/// prompt, and that can be told to report a failure for a given busy message.
/// </summary>
/// <remarks>
/// Keeps ViewModel tests focused on what the ViewModel does with each outcome, rather than re-testing the
/// runner's retry and progress behaviour — which is covered on its own. Failures are keyed by busy message so a
/// single test can let one load succeed while another fails, which is exactly the partial-failure case the
/// detail page has to handle.
/// </remarks>
public sealed class FakeBusyOperationRunner : IBusyOperationRunner
{
	/// <summary>
	/// Gets the busy messages passed to this runner, in order.
	/// </summary>
	public List<string> BusyMessages { get; } = new();

	/// <summary>
	/// Gets the failures to report instead of running the operation, keyed by busy message.
	/// </summary>
	/// <remarks>
	/// A keyed operation is never invoked, matching the real runner's behaviour once the user has declined to
	/// retry.
	/// </remarks>
	public Dictionary<string, OperationFailureReason> FailuresByBusyMessage { get; } =
		new(StringComparer.Ordinal);

	/// <inheritdoc />
	public async Task<OperationOutcome<T>> RunAsync<T>(
		string busyMessage,
		Func<CancellationToken, Task<T>> operation,
		CancellationToken cancellationToken = default)
	{
		BusyMessages.Add(busyMessage);

		if (FailuresByBusyMessage.TryGetValue(busyMessage, out var reason))
		{
			return OperationOutcome<T>.Failure(reason);
		}

		try
		{
			return OperationOutcome<T>.Success(await operation(cancellationToken).ConfigureAwait(false));
		}
		catch (OperationCanceledException)
		{
			return OperationOutcome<T>.Failure(OperationFailureReason.Cancelled);
		}
		catch (Exception exception)
		{
			return OperationOutcome<T>.Failure(OperationFailureReason.Faulted, exception);
		}
	}
}
