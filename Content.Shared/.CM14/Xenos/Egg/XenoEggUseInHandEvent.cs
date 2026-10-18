namespace Content.Shared.CM14.Xenonids.Egg;

public sealed partial class XenoEggUseInHandEvent : HandledEntityEventArgs
{
    public NetEntity UsedEgg;

    public XenoEggUseInHandEvent(NetEntity usedEgg)
    {
        UsedEgg = usedEgg;
    }
}
