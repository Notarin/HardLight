using Robust.Shared.Containers;

namespace Content.Shared._Goobstation.Factory.Slots;

/// <summary>
/// Abstraction over a <see cref="BaseContainer"/> on the machine.
/// </summary>
public sealed partial class AutomatedContainer : AutomationSlot
{
    /// <summary>
    /// The ID of the container to use.
    /// </summary>
    [DataField(required: true)]
    public string ContainerId = string.Empty;

    [DataField(required: true)]
    public int MaxItems;

    public SharedContainerSystem ContainerSystem;

    public BaseContainer Container;

    public override void Initialize()
    {
        base.Initialize();

        ContainerSystem = EntMan.System<SharedContainerSystem>();

        Container = ContainerSystem.GetContainer(Owner, ContainerId);
    }

    public override bool Insert(EntityUid item)
    {
        return base.Insert(item) && ContainerSystem.Insert(item, Container);
    }

    public override bool CanInsert(EntityUid item)
    {
        return base.CanInsert(item)
            && Container.Count < MaxItems
            && ContainerSystem.CanInsert(item, Container);
    }

    public override EntityUid? GetItem(EntityUid? filter)
    {
        foreach (var item in Container.ContainedEntities)
        {
            if (Filter.IsAllowed(filter, item))
                return item;
        }

        return null;
    }
}
