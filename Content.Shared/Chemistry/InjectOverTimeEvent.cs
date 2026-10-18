namespace Content.Shared.Chemistry.Events;

/// <summary>
/// Raised directed on an entity when it embeds in another entity.
/// </summary>
/// <param name="EmbeddedIntoUid">Entity that is embedded in.</param>
[ByRefEvent]
public readonly record struct InjectOverTimeEvent(EntityUid EmbeddedIntoUid);

