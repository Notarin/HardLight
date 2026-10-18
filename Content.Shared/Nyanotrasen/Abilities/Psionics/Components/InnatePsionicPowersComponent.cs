namespace Content.Shared.Nyanotrasen.Abilities.Psionics.Components
{
    /// <summary>
    /// Component for entities that have innate psionic powers that should be added on initialization.
    /// </summary>
    [RegisterComponent]
    public sealed partial class InnatePsionicPowersComponent : Component
    {
        /// <summary>
        /// List of psionic powers to add to the entity when this component is initialized.
        /// </summary>
        [DataField("powersToAdd")]
        public List<string> PowersToAdd = new();
    }
}
