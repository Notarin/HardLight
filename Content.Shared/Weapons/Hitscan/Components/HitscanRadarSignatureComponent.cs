// SPDX-FileCopyrightText: 2025 Avalon
//
// SPDX-License-Identifier: MPL-2.0

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// Stub component indicating a hitscan should emit a radar blip/signature when fired.
/// Prototype fields: radarColor (Color), lifeTime (seconds, optional).
/// </summary>
[RegisterComponent]
public sealed partial class HitscanRadarSignatureComponent : Component
{
    [DataField("radarColor")]
    public Color? RadarColor { get; set; }

    [DataField("lifeTime")]
    public float LifeTime { get; set; } = 0f;
}
