using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Prototypes;

namespace Content.Server.NPC.Components
{
    /// <summary>
    /// Allows clothing to add a faction to you when you wear it.
    /// </summary>
    [RegisterComponent]
    public sealed partial class ClothingAddFactionComponent : Component
    {
        public bool IsActive = false;

        /// <summary>
        /// Faction added
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite),
         DataField("faction", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<NpcFactionPrototype>))]
        public string Faction = "";
    }
}
