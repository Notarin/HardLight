using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared.Chemistry.Reagent;


/// <summary>
/// Handle taints in reagent. It can be tested by the TaintedCondition EntityEffectCondition.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class TaintData : ReagentData
{
    private TaintType _taint;

    [DataField(required: true)]
    public TaintType Taint 
    {
        get => _taint;
        set
        {
            if (!TaintType.ValidTaints.Contains(value))
                throw new ArgumentException($"{value} is not a valid Taint");
            _taint = value;
        }
    }

    public override ReagentData Clone() => new TaintData(Taint);

    public override bool Equals(ReagentData? other)
    {
        //I'll make it safe for any other ReagentData to make comparisons more simples.
        return other is TaintData taintData && taintData.Taint == Taint;
    }

    public override int GetHashCode()
    {
        return Taint.GetHashCode();
    }

    public TaintData(TaintType taint)
    {
        Taint = taint;
    }

    public override string ToString() => Taint;
}

/// <summary>
/// Class made to collect constants for easier references. 
/// Implicitely a string.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct TaintType(string Represent)
{
    //public static readonly TaintType EXAMPLE = new("example");

    public static readonly HashSet<TaintType> ValidTaints =
    [

    ];

    public static implicit operator TaintType(string represent) => new(represent);
    public static implicit operator string(TaintType taint) => taint.Represent;
}

/// <summary>
/// Type serializer for the thing that is a string. It is a string.
/// </summary>
[TypeSerializer]
public sealed class TaintTypeSerializer : ITypeSerializer<TaintType, ValueDataNode>
{
    public TaintType Read(ISerializationManager serializationManager,
    ValueDataNode node, IDependencyCollection dependencies,
    SerializationHookContext hookCtx,
    ISerializationContext? context = null,
    ISerializationManager.InstantiationDelegate<TaintType>? instanceProvider = null)
    {
        return new TaintType(node.Value);
    }

    public ValidationNode Validate(ISerializationManager serializationManager,
    ValueDataNode node,
    IDependencyCollection dependencies,
    ISerializationContext? context = null)
    {
        return TaintType.ValidTaints.Contains(node.Value)
        ? new ValidatedValueNode(node)
        : new ErrorNode(node, $@"""{node.Value}"" is not a valid taint. Is it added to TaintType ?");
    }

    public DataNode Write(ISerializationManager serializationManager,
    TaintType value,
    IDependencyCollection dependencies,
    bool alwaysWrite = false,
    ISerializationContext? context = null)
    {
        return new ValueDataNode(value.Represent);
    }
}