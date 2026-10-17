using Content.Server.Atmos.Piping.EntitySystems;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Piping.Components;

[RegisterComponent]
public sealed partial class AtmosPipeColorComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Color Color { get; set; } = Color.White;
}

[ByRefEvent]
public record struct AtmosPipeColorChangedEvent(Color color)
{
    public Color Color = color;
}
