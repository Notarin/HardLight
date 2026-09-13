using Robust.Shared.GameStates;

namespace Content.Shared.Parallax.Biomes;

/// <summary>
/// Added to procedural biome entities. If removed, the entity is considered modified.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BiomeUntouchedComponent : Component;
