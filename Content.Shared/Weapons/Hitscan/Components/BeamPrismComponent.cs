// SPDX-FileCopyrightText: 2026 HardLight contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Weapons.Hitscan.Components;

[RegisterComponent]
public sealed partial class BeamPrismComponent : Component
{
    public TimeSpan FiringOverlayExpireTime = TimeSpan.Zero;
    public uint NextFiringToken;
}
