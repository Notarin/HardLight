using Robust.Shared.Audio;


namespace Content.Server.Vampiric
{
    [RegisterComponent]
    public sealed partial class BloodSuckerComponent : Component
    {
        /// <summary>
        /// How much to succ each time we succ.
        /// </summary>
        [DataField("unitsToSucc")]
        public float UnitsToSucc = 20f;

        /// <summary>
        /// The time (in seconds) that it takes to succ an entity.
        /// </summary>
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public TimeSpan Delay = TimeSpan.FromSeconds(4);
        // Hardlight unused - start
        // ***INJECT WHEN SUCC***

        // Whether to inject chems into a chemstream when we suck something.
        // [DataField("injectWhenSucc")]
        // public bool InjectWhenSucc = false;

        // How many units of our injected chem to inject.
        // [DataField("unitsToInject")]
        // public float UnitsToInject = 5;

        // Which reagent to inject.
        // [DataField("injectReagent")]
        // public string InjectReagent = "";

        // Whether we need to web the thing up first...
        // [DataField("webRequired")]
        // public bool WebRequired = false;

        //     DEN: Used to track BloodExaminer, but only if it is added by this component.
        // public BloodExaminerComponent? AddedBloodExaminer;
        // Hardlight unused - end
        /// <summary>
        /// The sound made when drinking blood.
        /// </summary>
        public SoundSpecifier DrinkSound = new SoundPathSpecifier("/Audio/Items/drink.ogg");
    }
}
