using System.Linq;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.EntityEffects.EffectConditions;


/// <summary>
/// Checks if the reagent associated with the effect carries a set of taints.
/// </summary>
public sealed partial class TaintedCondition : EntityEffectCondition
{
    [DataField]
    public List<TaintType> Taints = default!;

    [DataField]
    public bool Blacklist = false;

    [DataField]
    public bool AllRequired = false;

    private ISawmill _sawmill = default!;

    public override bool Condition(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs)
        {
            _sawmill = Logger.GetSawmill("tainted condtion");
            _sawmill.Warning($"Raised for a non reagent effect {args.GetType()} ! Defaulting to false.");
            return false;
        }


        //Okay so this uses reagentprototype instead of reagentQuantity
        var solution = reagentArgs.Source;
        if (solution is null) return Blacklist;

        var reagentdata = solution
        .FirstOrNull(x => x.Reagent.Prototype == reagentArgs.Reagent?.ID)?
        .Reagent.EnsureReagentData();

        if (reagentdata is null) return Blacklist;


        var tocheck = Taints.ConvertAll(x => new TaintData(x));

        var check = AllRequired
        ? tocheck.All(reagentdata.Contains)
        : tocheck.Any(reagentdata.Contains);

        return check != Blacklist;
    }

    public override string GuidebookExplanation(IPrototypeManager prototype)
    {
        return Loc.GetString("reagent-effect-condition-guidebook-taint", ("taint", string.Join(",", Taints)),
                                                                          ("blacklist", Blacklist),
                                                                          ("all", AllRequired));
    }
}
