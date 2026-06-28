//linq
using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.Weapon.Components;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Starlight.Weapon.Systems;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Client.Starlight.Weapon.Systems;

public sealed partial class WeaponDismantleOnShootSystem : SharedWeaponDismantleOnShootSystem { }
