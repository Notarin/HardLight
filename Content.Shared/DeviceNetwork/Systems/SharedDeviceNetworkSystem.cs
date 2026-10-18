using Content.Shared.DeviceNetwork.Components;

namespace Content.Shared.DeviceNetwork.Systems;

public abstract class SharedDeviceNetworkSystem : EntitySystem
{
    /// <summary>
    /// Sends the given payload as a device network packet to the entity with the given address and frequency.
    /// Addresses are given to the DeviceNetworkComponent of an entity when connecting.
    /// </summary>
    /// <returns>Returns true when the packet was successfully enqueued.</returns>
    public virtual bool QueuePacket(EntityUid uid,
        string? address,
        NetworkPayload data,
        uint? frequency = null,
        int? network = null,
        DeviceNetworkComponent? device = null)
    {
        return false;
    }
}
