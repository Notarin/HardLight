namespace Content.Server.Shuttles.Events;

/// <summary>
/// Raised when a shuttle console is trying to FTL via UI input.
/// </summary>
[ByRefEvent]
public record struct ConsoleFTLAttemptEvent(EntityUid Uid, bool Cancelled, string Reason);
