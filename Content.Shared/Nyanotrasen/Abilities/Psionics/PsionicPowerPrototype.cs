using Robust.Shared.Prototypes;

namespace Content.Shared.Abilities.Psionics
{
    /// <summary>
    ///     Prototype for defining psionic powers and their properties.
    /// </summary>
    [Prototype]
    public sealed partial class PsionicPowerPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField("name")]
        public string Name { get; private set; } = string.Empty;

        [DataField("description")]
        public string Description { get; private set; } = string.Empty;

        [DataField("amplificationModifier")]
        public float AmplificationModifier { get; private set; } = 0f;

        [DataField("dampeningModifier")]
        public float DampeningModifier { get; private set; } = 0f;

        [DataField("powerSlotCost")]
        public int PowerSlotCost { get; private set; } = 1;

        [DataField("initializationFeedback")]
        public string? InitializationFeedback { get; private set; }

        [DataField("metapsionicFeedback")]
        public string? MetapsionicFeedback { get; private set; }

        [DataField("actions")]
        public List<string> Actions { get; private set; } = new();

        [DataField("components")]
        public List<string> Components { get; private set; } = new();
    }
}
