using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Shared._Goobstation.Factory.Slots;

/// <summary>
/// Adds no item I/O, only enables signal ports.
/// </summary>
public sealed partial class AutomatedPorts : AutomationSlot
{
    [DataField]
    public ProtoId<SinkPortPrototype>[] Sinks = [];

    [DataField]
    public ProtoId<SourcePortPrototype>[] Sources = [];

    public override void AddPorts()
    {
        base.AddPorts();

        Device.EnsureSinkPorts(Owner, Sinks);
        Device.EnsureSourcePorts(Owner, Sources);
    }

    public override void RemovePorts()
    {
        base.RemovePorts();

        foreach (var port in Sinks)
        {
            Device.RemoveSinkPort(Owner, port);
        }
        foreach (var port in Sources)
        {
            Device.RemoveSourcePort(Owner, port);
        }
    }
}
