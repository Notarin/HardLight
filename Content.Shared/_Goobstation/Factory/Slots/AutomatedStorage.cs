using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;

namespace Content.Shared._Goobstation.Factory.Slots;

/// <summary>
/// Abstraction over a <see cref="StorageComponent"/> grid inventory.
/// </summary>
public sealed partial class AutomatedStorage : AutomationSlot
{
    public SharedStorageSystem Storage;
    public StorageComponent Comp;

    public override void Initialize()
    {
        base.Initialize();

        Storage = EntMan.System<SharedStorageSystem>();
        Comp = EntMan.GetComponent<StorageComponent>(Owner);
    }

    public override bool Insert(EntityUid item)
    {
        return base.Insert(item) &&
            Storage.Insert(Owner, item, out _, storageComp: Comp);
    }

    public override bool CanInsert(EntityUid item)
    {
        return base.CanInsert(item) &&
            Storage.CanInsert(Owner, item, out _, storageComp: Comp);
    }

    public override EntityUid? GetItem(EntityUid? filter)
    {
        foreach (var item in Comp.Container.ContainedEntities)
        {
            if (Filter.IsAllowed(filter, item))
                return item;
        }

        return null;
    }
}
