using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._HL.Insurance;
using Content.Shared._NF.Bank;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._HL.Insurance.UI;

public sealed class ParadoxGeneratorWindow : FancyWindow
{
    public event Action? OnRefresh;
    public event Action? OnInsure;
    public event Action? OnItemSlot;
    public event Action<string>? OnClaim;

    private readonly Label _balanceLabel;
    private readonly Label _insertedLabel;
    private readonly Label _costLabel;
    private readonly Button _itemSlotButton;
    private readonly Button _insureButton;
    private readonly Button _refreshButton;
    private readonly BoxContainer _list;

    public ParadoxGeneratorWindow()
    {
        Title = "Paradox Generator";
        MinSize = new Vector2(520, 420);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
        };

        ContentsContainer.AddChild(root);

        _balanceLabel = new Label();
        root.AddChild(_balanceLabel);

        _insertedLabel = new Label();
        root.AddChild(_insertedLabel);

        _costLabel = new Label();
        root.AddChild(_costLabel);

        var controls = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        root.AddChild(controls);

        _itemSlotButton = new Button { Text = "Insert / Eject" };
        _itemSlotButton.OnPressed += _ => OnItemSlot?.Invoke();
        controls.AddChild(_itemSlotButton);

        _insureButton = new Button { Text = "Insure" };
        _insureButton.OnPressed += _ => OnInsure?.Invoke();
        controls.AddChild(_insureButton);

        _refreshButton = new Button { Text = "Refresh" };
        _refreshButton.OnPressed += _ => OnRefresh?.Invoke();
        controls.AddChild(_refreshButton);

        root.AddChild(new Label { Text = "Policies" });

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
        };
        root.AddChild(scroll);

        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };
        scroll.AddChild(_list);
    }

    public void SetState(ParadoxGeneratorBoundUserInterfaceState state)
    {
        _balanceLabel.Text = $"Balance: {BankSystemExtensions.ToSpesoString(state.Balance)}";
        _insertedLabel.Text = state.HasInsertedItem
            ? $"Inserted: {state.InsertedItemName}"
            : "Inserted: none";

        _costLabel.Text = state.HasInsertedItem
            ? $"Value: {BankSystemExtensions.ToSpesoString(state.InsertedItemValue)} | Premium: {BankSystemExtensions.ToSpesoString(state.InsertedItemPremium)}"
            : "Value: n/a";

        _insureButton.Disabled = !state.HasInsertedItem || state.InsertedItemPremium <= 0;

        _list.RemoveAllChildren();
        if (state.Listings.Count == 0)
        {
            _list.AddChild(new Label { Text = "No insured items." });
            return;
        }

        foreach (var listing in state.Listings)
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
            };

            var label = new Label
            {
                Text = $"{listing.Name} | claim {BankSystemExtensions.ToSpesoString(listing.ClaimCost)} | gen {listing.Generation}",
                HorizontalExpand = true,
                ClipText = true,
            };
            row.AddChild(label);

            var claim = new Button
            {
                Text = listing.LiveCurrentCopyExists ? "Exists" : "Claim",
                Disabled = listing.LiveCurrentCopyExists,
            };

            var policyId = listing.PolicyId;
            claim.OnPressed += _ => OnClaim?.Invoke(policyId);
            row.AddChild(claim);

            _list.AddChild(row);
        }
    }
}
