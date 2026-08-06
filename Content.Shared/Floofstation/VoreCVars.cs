using Robust.Shared.Configuration;

namespace Content.Shared.FloofStation;

[CVarDefs]
public sealed class VoreCVars
{
    // Hardlight - Start
    /// <summary>
    /// Enables the vore container interaction verbs such as devour, self-insert, and release.
    /// </summary>
    // Hardlight - End
    public static readonly CVarDef<bool> VoreEnabled =
        CVarDef.Create("game.vore_enabled", true, CVar.SERVER | CVar.REPLICATED);

    // Hardlight - Start
    /// <summary>
    /// Enables digestion verbs and digestion processing for prey that opted into digestion consent.
    /// </summary>
    // Hardlight - End
    public static readonly CVarDef<bool> DigestionEnabled =
        CVarDef.Create("game.digestion_enabled", true, CVar.SERVER | CVar.REPLICATED);
}

