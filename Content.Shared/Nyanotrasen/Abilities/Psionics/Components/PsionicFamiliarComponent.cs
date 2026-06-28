using Content.Shared.Abilities.Psionics;
using Content.Shared.Nyanotrasen.Abilities.Psionics;
using Content.Shared.Popups;
using Robust.Shared.Analyzers;
using Robust.Shared.GameStates;

namespace Content.Shared.Nyanotrasen.Abilities.Psionics.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedPsionicAbilitiesSystem), Other = AccessPermissions.ReadWriteExecute)]
public sealed partial class PsionicFamiliarComponent : Component
{
    [DataField, Access(typeof(SharedPsionicAbilitiesSystem), Other = AccessPermissions.ReadWriteExecute)]
    public EntityUid Master = EntityUid.Invalid;

    [DataField]
    public bool InheritMasterFactions = true;

    [DataField]
    public bool CanAttackMaster = false;

    [DataField]
    public string AttackMasterText = "psionic-familiar-cant-attack-master";

    [DataField]
    public PopupType AttackPopupType = PopupType.Medium;

    [DataField]
    public bool DespawnOnFamiliarDeath = true;

    [DataField]
    public bool DespawnOnMasterDeath = true;

    [DataField]
    public string DespawnText = "psionic-familiar-despawned";

    [DataField]
    public PopupType DespawnPopopType = PopupType.Medium;
}
