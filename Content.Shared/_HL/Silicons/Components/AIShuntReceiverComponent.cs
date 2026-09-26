using Robust.Shared.GameStates;

namespace Content.Shared._HL.Silicons.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class AIShuntReceiverComponent : Component
    {
        [ViewVariables]
        [DataField]
        public EntityUid? AssignedGrid = null;
    }
}
