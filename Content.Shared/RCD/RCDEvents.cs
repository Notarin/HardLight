using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Atmos.Components; // Starlighgt: RPD

namespace Content.Shared.RCD;

[Serializable, NetSerializable]
public sealed class RCDSystemMessage(ProtoId<RCDPrototype> protoId) : BoundUserInterfaceMessage
{
    public ProtoId<RCDPrototype> ProtoId = protoId;
}

[Serializable, NetSerializable]
public sealed class RCDConstructionGhostRotationEvent(NetEntity netEntity, Direction direction) : EntityEventArgs
{
    public readonly NetEntity NetEntity = netEntity;
    public readonly Direction Direction = direction;
}

// Starlighgt Start: RPD
[Serializable, NetSerializable]
public sealed class RCDConstructionGhostLayerEvent(NetEntity netEntity, AtmosPipeLayer layer) : EntityEventArgs
{
    public readonly NetEntity NetEntity = netEntity;
    public readonly AtmosPipeLayer Layer = layer;
}

[Serializable, NetSerializable]
public sealed class RCDConstructionGhostFlipEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;
    public readonly bool UseMirrorPrototype;
    public RCDConstructionGhostFlipEvent(NetEntity netEntity, bool useMirrorPrototype)
    {
        NetEntity = netEntity;
        UseMirrorPrototype = useMirrorPrototype;
    }
}
// Starlight End

[Serializable, NetSerializable]
public enum RcdUiKey : byte
{
    Key
}

// Starlight: RPLD
[Serializable, NetSerializable]
public sealed class RPDSelectedLayerEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;
    public readonly byte Layer;

    public RPDSelectedLayerEvent(NetEntity netEntity, byte layer)
    {
        NetEntity = netEntity;
        Layer = layer;
    }
}


[Serializable, NetSerializable]
public sealed class RPDEyeRotationEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;
    public float? EyeRotation;

    public RPDEyeRotationEvent(NetEntity netEntity, float? eyeRotation)
    {
        NetEntity = netEntity;
        EyeRotation = eyeRotation;
    }
}
// Starlight End: RPD
