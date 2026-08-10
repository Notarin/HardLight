using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._HL.Insurance;
using Content.Shared._NF.Bank;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._HL.Insurance.UI;

public sealed class ParadoxGeneratorWindow : FancyWindow
{
    public event Action? OnRefresh;
    public event Action? OnInsure;
    public event Action? OnItemSlot;
    public event Action<string>? OnClaim;
    public event Action<string>? OnUninsure;

    private readonly Label _balanceLabel;
    private readonly Label _insertedLabel;
    private readonly Label _costLabel;
    private readonly Button _itemSlotButton;
    private readonly Button _insureButton;
    private readonly Button _refreshButton;
    private readonly LineEdit _searchBar;
    private readonly BoxContainer _list;
    private readonly Label _policiesLabel;
    private ParadoxGeneratorBoundUserInterfaceState? _lastState;
    private static readonly Color PolicyRowColor = Color.FromHex("#303246");
    private static readonly Color PolicyRowUnavailableColor = Color.FromHex("#282936");

    public ParadoxGeneratorWindow()
    {
        Title = Loc.GetString("paradox-generator-window-title");
        MinSize = new Vector2(560, 440);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            Margin = new Thickness(4, 4),
        };

        ContentsContainer.AddChild(root);

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        root.AddChild(header);

        _balanceLabel = new Label
        {
            HorizontalExpand = true,
        };
        header.AddChild(_balanceLabel);

        _refreshButton = new Button { Text = Loc.GetString("paradox-generator-refresh-button") };
        _refreshButton.OnPressed += _ => OnRefresh?.Invoke();
        header.AddChild(_refreshButton);

        _insertedLabel = new Label
        {
            ClipText = true,
        };
        root.AddChild(_insertedLabel);

        _costLabel = new Label();
        root.AddChild(_costLabel);

        var controls = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        root.AddChild(controls);

        _itemSlotButton = new Button { Text = Loc.GetString("paradox-generator-insert-button") };
        _itemSlotButton.OnPressed += _ => OnItemSlot?.Invoke();
        controls.AddChild(_itemSlotButton);

        _insureButton = new Button { Text = Loc.GetString("paradox-generator-insure-button") };
        _insureButton.OnPressed += _ => OnInsure?.Invoke();
        controls.AddChild(_insureButton);

        _policiesLabel = new Label();
        root.AddChild(_policiesLabel);

        _searchBar = new LineEdit
        {
            PlaceHolder = Loc.GetString("paradox-generator-search-placeholder"),
            Margin = new Thickness(0, 4),
        };
        _searchBar.OnTextChanged += _ => RenderPolicyList();
        root.AddChild(_searchBar);

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
        _lastState = state;

        _balanceLabel.Text = Loc.GetString("paradox-generator-balance",
            ("balance", BankSystemExtensions.ToSpesoString(state.Balance)));
        _insertedLabel.Text = state.HasInsertedItem
            ? Loc.GetString("paradox-generator-inserted-item", ("item", state.InsertedItemName ?? string.Empty))
            : Loc.GetString("paradox-generator-inserted-item-none");

        _costLabel.Text = state.HasInsertedItem && state.InsertedItemValue > 0
            ? Loc.GetString("paradox-generator-inserted-cost",
                ("value", BankSystemExtensions.ToSpesoString(state.InsertedItemValue)),
                ("premium", BankSystemExtensions.ToSpesoString(state.InsertedItemPremium)))
            : Loc.GetString("paradox-generator-inserted-cost-none");

        _itemSlotButton.Text = state.HasInsertedItem
            ? Loc.GetString("paradox-generator-eject-button")
            : Loc.GetString("paradox-generator-insert-button");

        var canAffordPremium = state.Balance >= state.InsertedItemPremium;
        var canInsureInserted = string.IsNullOrEmpty(state.InsertedItemCannotInsureReason);
        _insureButton.Disabled = !state.HasInsertedItem || !canInsureInserted || state.InsertedItemPremium <= 0 || !canAffordPremium;
        _insureButton.ToolTip = _insureButton.Disabled
            ? !state.HasInsertedItem
                ? Loc.GetString("paradox-generator-insure-tooltip-no-item")
                : !canInsureInserted
                    ? GetCannotInsureReason(state.InsertedItemCannotInsureReason)
                    : !canAffordPremium
                        ? Loc.GetString("paradox-generator-insure-tooltip-insufficient-funds")
                        : GetCannotInsureReason(state.InsertedItemCannotInsureReason)
            : null;

        RenderPolicyList();
    }

    private void RenderPolicyList()
    {
        if (_lastState == null)
            return;

        var state = _lastState;
        var search = _searchBar.Text.Trim();
        var listings = state.Listings
            .Where(listing => MatchesSearch(listing, search))
            .OrderBy(listing => listing.Name)
            .ToList();

        _policiesLabel.Text = string.IsNullOrEmpty(search)
            ? Loc.GetString("paradox-generator-policies", ("count", state.Listings.Count))
            : Loc.GetString("paradox-generator-policies-filtered",
                ("count", listings.Count),
                ("total", state.Listings.Count));

        _list.RemoveAllChildren();
        if (state.Listings.Count == 0)
        {
            _list.AddChild(new Label
            {
                Text = Loc.GetString("paradox-generator-no-policies"),
                Margin = new Thickness(4, 4),
            });
            return;
        }

        if (listings.Count == 0)
        {
            _list.AddChild(new Label
            {
                Text = Loc.GetString("paradox-generator-no-search-results"),
                Margin = new Thickness(4, 4),
            });
            return;
        }

        foreach (var listing in listings)
        {
            var canAfford = state.Balance >= listing.ClaimCost;
            var canClaim = !listing.LiveCurrentCopyExists && canAfford;

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                Margin = new Thickness(4, 4),
            };

            var details = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                SeparationOverride = 2,
            };
            row.AddChild(details);

            details.AddChild(new Label
            {
                Text = listing.Name,
                HorizontalExpand = true,
                ClipText = true,
            });

            details.AddChild(new Label
            {
                Text = Loc.GetString("paradox-generator-policy-cost",
                    ("value", BankSystemExtensions.ToSpesoString(listing.InsuredValue)),
                    ("claim", BankSystemExtensions.ToSpesoString(listing.ClaimCost))),
                HorizontalExpand = true,
                ClipText = true,
            });

            var claim = new Button
            {
                Text = Loc.GetString("paradox-generator-claim-button"),
                Disabled = !canClaim,
                ToolTip = !canClaim
                    ? listing.LiveCurrentCopyExists
                        ? Loc.GetString("paradox-generator-claim-tooltip-live-copy")
                        : Loc.GetString("paradox-generator-claim-tooltip-insufficient-funds")
                    : null,
            };

            var policyId = listing.PolicyId;
            claim.OnPressed += _ => OnClaim?.Invoke(policyId);

            var uninsure = new Button
            {
                Text = Loc.GetString("paradox-generator-remove-policy-button"),
                ToolTip = Loc.GetString("paradox-generator-remove-policy-tooltip"),
            };
            uninsure.OnPressed += _ => OnUninsure?.Invoke(policyId);

            var actions = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 4,
            };
            actions.AddChild(claim);
            actions.AddChild(uninsure);
            row.AddChild(actions);

            _list.AddChild(new PanelContainer
            {
                PanelOverride = new StyleBoxFlat(canClaim ? PolicyRowColor : PolicyRowUnavailableColor),
                Children =
                {
                    row,
                },
            });
        }
    }

    private static bool MatchesSearch(InsuranceListingState listing, string search)
    {
        return string.IsNullOrEmpty(search) ||
               listing.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
               listing.PolicyId.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    public void SetPendingState()
    {
        _lastState = null;
        _balanceLabel.Text = Loc.GetString("paradox-generator-loading");
        _insertedLabel.Text = Loc.GetString("paradox-generator-inserted-item-none");
        _costLabel.Text = Loc.GetString("paradox-generator-inserted-cost-none");
        _itemSlotButton.Text = Loc.GetString("paradox-generator-insert-button");
        _insureButton.Disabled = true;
        _insureButton.ToolTip = Loc.GetString("paradox-generator-insure-tooltip-no-item");
        _policiesLabel.Text = Loc.GetString("paradox-generator-policies", ("count", 0));
        _list.RemoveAllChildren();
        _list.AddChild(new Label
        {
            Text = Loc.GetString("paradox-generator-loading"),
            Margin = new Thickness(4, 4),
        });
    }

    private static string GetCannotInsureReason(string? reason)
    {
        return string.IsNullOrEmpty(reason)
            ? Loc.GetString("paradox-generator-insure-tooltip-invalid-item")
            : Loc.GetString(reason);
    }
}
