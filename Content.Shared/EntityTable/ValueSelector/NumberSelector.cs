using Content.Shared.EntityTable.EntitySelectors;
using JetBrains.Annotations;
using Robust.Shared.Random;

namespace Content.Shared.EntityTable.ValueSelector;

/// <summary>
/// Used for implementing custom value selection for <see cref="EntityTableSelector"/>
/// </summary>
[ImplicitDataDefinitionForInheritors, UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract partial class NumberSelector
{
    public abstract int Get(IRobustRandom rand);
}
