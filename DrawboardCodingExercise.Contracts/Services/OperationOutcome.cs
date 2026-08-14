using System;

namespace DrawboardCodingExercise.Contracts.Services;

/// <summary>
/// Why a guarded operation did not produce a value.
/// </summary>
public enum OperationFailureReason
{
	/// <summary>The operation succeeded; no failure occurred.</summary>
	None,

	/// <summary>
	/// The operation failed and the user chose not to retry. The user has already been told, so callers
	/// should show a quiet inline error state rather than raising a second prompt.
	/// </summary>
	DeclinedByUser,

	/// <summary>
	/// The operation was abandoned because its cancellation token was signalled, normally because the user
	/// navigated away. No error should be shown — nobody is waiting for the result.
	/// </summary>
	Cancelled,

	/// <summary>
	/// The operation threw an exception that the runner does not treat as retryable. The exception is
	/// available on <see cref="OperationOutcome{T}.Exception"/> and has been logged.
	/// </summary>
	Faulted
}

/// <summary>
/// The result of an operation run through <see cref="IBusyOperationRunner"/>: either a value, or a reason
/// no value was produced.
/// </summary>
/// <remarks>
/// Returned instead of throwing so that ViewModels handle failure with a branch rather than a try/catch,
/// which keeps their tests straightforward and stops retry handling from leaking into every call site.
/// </remarks>
/// <typeparam name="T">The type of value the operation produces.</typeparam>
public sealed class OperationOutcome<T>
{
	private OperationOutcome(bool isSuccessful, T value, OperationFailureReason failureReason, Exception? exception)
	{
		IsSuccessful = isSuccessful;
		Value = value;
		FailureReason = failureReason;
		Exception = exception;
	}

	/// <summary>
	/// Gets a value indicating whether the operation produced a result.
	/// </summary>
	public bool IsSuccessful { get; }

	/// <summary>
	/// Gets the value the operation produced.
	/// </summary>
	/// <value>
	/// The result when <see cref="IsSuccessful"/> is <see langword="true"/>; otherwise
	/// <see langword="default"/>, which callers must not treat as data.
	/// </value>
	public T Value { get; }

	/// <summary>
	/// Gets the reason no value was produced, or <see cref="OperationFailureReason.None"/> on success.
	/// </summary>
	public OperationFailureReason FailureReason { get; }

	/// <summary>
	/// Gets the exception that caused the failure, when one is available.
	/// </summary>
	/// <value>
	/// The originating exception for <see cref="OperationFailureReason.DeclinedByUser"/> and
	/// <see cref="OperationFailureReason.Faulted"/>; otherwise <see langword="null"/>. Already logged by the
	/// runner, so callers need not log it again.
	/// </value>
	public Exception? Exception { get; }

	/// <summary>
	/// Creates a successful outcome.
	/// </summary>
	/// <param name="value">The value the operation produced.</param>
	/// <returns>A successful outcome wrapping <paramref name="value"/>.</returns>
	public static OperationOutcome<T> Success(T value) =>
		new(true, value, OperationFailureReason.None, null);

	/// <summary>
	/// Creates a failed outcome.
	/// </summary>
	/// <param name="reason">Why no value was produced.</param>
	/// <param name="exception">The originating exception, where one applies.</param>
	/// <returns>A failed outcome carrying <paramref name="reason"/>.</returns>
	public static OperationOutcome<T> Failure(OperationFailureReason reason, Exception? exception = null) =>
		new(false, default!, reason, exception);
}
