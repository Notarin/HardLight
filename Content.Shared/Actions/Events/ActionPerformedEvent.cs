namespace Content.Shared.Actions.Events;

/// <summary>
///     Raised on the action entity when it is used and BaseActionEvent.Handled.
/// </summary>
/// <param name="Performer">The entity that performed this action.</param>
[ByRefEvent]
public readonly record struct ActionPerformedEvent(EntityUid Performer);
