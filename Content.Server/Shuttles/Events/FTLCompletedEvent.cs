namespace Content.Server.Shuttles.Events;

/// <summary>
/// Raised when ShuttleSystem.FasterThanLight has completed FTL Travel.
/// </summary>
[ByRefEvent]
public readonly record struct FTLCompletedEvent(EntityUid Entity, EntityUid MapUid);
