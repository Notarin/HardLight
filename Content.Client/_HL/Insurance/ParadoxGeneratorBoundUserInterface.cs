using Content.Client._HL.Insurance.UI;
using Content.Shared._HL.Insurance;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;

namespace Content.Client._HL.Insurance;

public sealed class ParadoxGeneratorBoundUserInterface : BoundUserInterface
{
    private ParadoxGeneratorWindow? _window;

    public ParadoxGeneratorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ParadoxGeneratorWindow>();
        _window.OnRefresh += () => SendMessage(new ParadoxGeneratorRefreshMessage());
        _window.OnInsure += () => SendMessage(new ParadoxGeneratorInsureMessage());
        _window.OnClaim += policyId => SendMessage(new ParadoxGeneratorClaimMessage(policyId));
        _window.OnItemSlot += () => SendMessage(new ItemSlotButtonPressedEvent("paradox-generator-item"));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ParadoxGeneratorBoundUserInterfaceState generatorState)
            _window?.SetState(generatorState);
    }
}
