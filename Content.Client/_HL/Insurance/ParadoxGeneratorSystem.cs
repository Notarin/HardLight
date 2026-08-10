using Content.Shared._HL.Insurance;
using Robust.Client.GameObjects;

namespace Content.Client._HL.Insurance;

public sealed class ParadoxGeneratorSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ParadoxGeneratorUiStateMessage>(OnState);
    }

    private void OnState(ParadoxGeneratorUiStateMessage message)
    {
        var generator = GetEntity(message.Generator);
        if (!_ui.TryGetOpenUi<ParadoxGeneratorBoundUserInterface>(generator, ParadoxGeneratorUiKey.Key, out var bui))
            return;

        bui.SetGeneratorState(message.State);
    }
}
