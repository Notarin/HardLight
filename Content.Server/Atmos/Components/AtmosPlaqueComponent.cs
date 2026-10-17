using Content.Server.Atmos.EntitySystems;

namespace Content.Server.Atmos.Components
{
    [RegisterComponent]
    public sealed partial class AtmosPlaqueComponent : Component
    {
        [DataField("plaqueType"), ViewVariables(VVAccess.ReadWrite)]
        public PlaqueType Type = PlaqueType.Unset;
    }
}
