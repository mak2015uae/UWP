namespace DrawboardCodingExercise.Contracts.Events;

/// <summary>
/// Announces that a long-running operation has finished, so the shell can clear its progress entry.
/// </summary>
/// <remarks>
/// Must carry exactly the same text as the <see cref="NotifyBusyEvent"/> it completes; an unmatched value
/// leaves the progress indicator visible. Post these through
/// <see cref="Services.IBusyOperationRunner"/> rather than by hand, which guarantees the pairing.
/// </remarks>
/// <param name="Event">The description used as the correlation key, matching the busy notification.</param>
public record NotifyDoneEvent(string Event);
