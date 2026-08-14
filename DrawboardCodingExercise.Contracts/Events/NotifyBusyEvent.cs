namespace DrawboardCodingExercise.Contracts.Events;

/// <summary>
/// Announces that a long-running operation has started, so the shell can show progress.
/// </summary>
/// <remarks>
/// The shell clears the entry by matching <see cref="Event"/> against the corresponding
/// <see cref="NotifyDoneEvent"/>, so the two strings must be identical. Post these through
/// <see cref="Services.IBusyOperationRunner"/> rather than by hand, which guarantees the pairing.
/// </remarks>
/// <param name="Event">
/// The already-localized description of the work, displayed to the user and used as the correlation key.
/// </param>
public record NotifyBusyEvent(string Event);
