namespace Content.Server.Abilities.Oni
{
    [RegisterComponent]
    public sealed partial class HeldByOniComponent : Component
    {
        public EntityUid Holder = default!;

        // Frontier: wield accuracy fix
        public double MinAngleAdded = 0.0;
        public double MaxAngleAdded = 0.0;
        public double AngleIncreaseAdded = 0.0;
        // End Frontier
    }
}
