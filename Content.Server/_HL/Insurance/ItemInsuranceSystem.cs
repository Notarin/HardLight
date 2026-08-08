using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Server._NF.Bank;
using Content.Server.Cargo.Systems;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared._HL.Insurance;
using Content.Shared._HL.Insurance.Components;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._HL.Insurance;

public sealed class ItemInsuranceSystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public const string SaveRoot = "/item_insurance";
    private const string PolicyExtension = ".json";
    private const string EntityExtension = ".yml";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ParadoxGeneratorComponent, ComponentInit>(OnGeneratorInit);
        SubscribeLocalEvent<ParadoxGeneratorComponent, ComponentRemove>(OnGeneratorRemove);
        SubscribeLocalEvent<ParadoxGeneratorComponent, BoundUIOpenedEvent>(OnGeneratorUiOpened);
        SubscribeLocalEvent<ParadoxGeneratorComponent, EntInsertedIntoContainerMessage>(OnGeneratorSlotChanged);
        SubscribeLocalEvent<ParadoxGeneratorComponent, EntRemovedFromContainerMessage>(OnGeneratorSlotChanged);

        SubscribeLocalEvent<ParadoxGeneratorComponent, ParadoxGeneratorRefreshMessage>(OnRefresh);
        SubscribeLocalEvent<ParadoxGeneratorComponent, ParadoxGeneratorInsureMessage>(OnInsure);
        SubscribeLocalEvent<ParadoxGeneratorComponent, ParadoxGeneratorClaimMessage>(OnClaim);
    }

    private void OnGeneratorInit(Entity<ParadoxGeneratorComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent.Owner, ParadoxGeneratorComponent.ItemSlotId, ent.Comp.ItemSlot);
    }

    private void OnGeneratorRemove(Entity<ParadoxGeneratorComponent> ent, ref ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(ent.Owner, ent.Comp.ItemSlot);
    }

    private void OnGeneratorUiOpened(Entity<ParadoxGeneratorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.Owner, ent.Comp, args.Actor);
    }

    private void OnGeneratorSlotChanged(Entity<ParadoxGeneratorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        OnGeneratorSlotChanged(ent, args);
    }

    private void OnGeneratorSlotChanged(Entity<ParadoxGeneratorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        OnGeneratorSlotChanged(ent, args);
    }

    private void OnGeneratorSlotChanged(Entity<ParadoxGeneratorComponent> ent, ContainerModifiedMessage args)
    {
        if (args.Container.ID != ent.Comp.ItemSlot.ContainerSlot?.ID)
            return;

        foreach (var actor in _ui.GetActors(ent.Owner, ParadoxGeneratorUiKey.Key))
            UpdateUi(ent.Owner, ent.Comp, actor);
    }

    private void OnRefresh(Entity<ParadoxGeneratorComponent> ent, ref ParadoxGeneratorRefreshMessage args)
    {
        if (!CanUseGenerator(ent.Owner, args.Actor))
            return;

        UpdateUi(ent.Owner, ent.Comp, args.Actor);
    }

    private void OnInsure(Entity<ParadoxGeneratorComponent> ent, ref ParadoxGeneratorInsureMessage args)
    {
        var actor = args.Actor;
        if (!CanUseGenerator(ent.Owner, actor))
            return;

        if (!TryGetSession(actor, out var session))
        {
            Popup(actor, "Unable to identify account.");
            return;
        }

        if (!TryGetCharacterKey(actor, session, out var characterKey))
        {
            Popup(actor, "Unable to identify character.");
            return;
        }

        if (ent.Comp.ItemSlot.ContainerSlot?.ContainedEntity is not { } item)
        {
            Popup(actor, "Insert an item first.");
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!CanInsureItem(item, out var reason))
        {
            Popup(actor, reason);
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!_bank.TryGetBalance(actor, out var balance))
        {
            Popup(actor, "Unable to read bank balance.");
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        var value = GetInsuredValue(item);
        var premium = CalculatePremium(ent.Comp, value, balance);
        var claimCost = CalculateClaimCost(ent.Comp, value, balance);

        var hadInsurance = TryComp<InsuredItemComponent>(item, out var previousInsurance);
        var previousPolicyId = previousInsurance?.PolicyId ?? string.Empty;
        var previousOwnerUserId = previousInsurance?.OwnerUserId ?? string.Empty;
        var previousOwnerCharacterKey = previousInsurance?.OwnerCharacterKey ?? string.Empty;
        var previousGeneration = previousInsurance?.Generation ?? 0;

        var insured = EnsureComp<InsuredItemComponent>(item);
        if (!TryGetPolicyId(insured, out var policyId))
        {
            policyId = Guid.NewGuid();
            insured.PolicyId = policyId.ToString("N");
        }

        insured.OwnerUserId = session.UserId.ToString();
        insured.OwnerCharacterKey = characterKey;

        if (TryReadPolicy(characterKey, policyId, out var existing))
            insured.Generation = existing.Generation;

        Dirty(item, insured);

        if (!TrySerializeItem(item, out var yaml))
        {
            RestoreInsuranceComponent(item, hadInsurance, previousPolicyId, previousOwnerUserId, previousOwnerCharacterKey, previousGeneration);
            Popup(actor, "The item resisted paradox encoding.");
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        var policy = new ItemInsurancePolicyRecord
        {
            PolicyId = policyId,
            OwnerUserId = insured.OwnerUserId,
            OwnerCharacterKey = insured.OwnerCharacterKey,
            Name = Name(item),
            Prototype = MetaData(item).EntityPrototype?.ID ?? string.Empty,
            Value = value,
            Premium = premium,
            ClaimCost = claimCost,
            Generation = insured.Generation,
            InsuredAt = DateTime.UtcNow,
        };

        if (!_bank.TryBankWithdraw(actor, premium))
        {
            RestoreInsuranceComponent(item, hadInsurance, previousPolicyId, previousOwnerUserId, previousOwnerCharacterKey, previousGeneration);
            Popup(actor, $"Insufficient funds. Premium: {premium} spesos.");
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!TryWritePolicy(policy, yaml))
        {
            _bank.TryBankDeposit(actor, premium);
            RestoreInsuranceComponent(item, hadInsurance, previousPolicyId, previousOwnerUserId, previousOwnerCharacterKey, previousGeneration);
            Popup(actor, "The policy failed to write.");
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        Popup(actor, $"Insured {policy.Name} for {premium} spesos.");
        UpdateUi(ent.Owner, ent.Comp, actor);
    }

    private void OnClaim(Entity<ParadoxGeneratorComponent> ent, ref ParadoxGeneratorClaimMessage args)
    {
        var actor = args.Actor;
        if (!CanUseGenerator(ent.Owner, actor))
            return;

        if (!TryGetSession(actor, out var session))
        {
            Popup(actor, "Unable to identify account.");
            return;
        }

        if (!TryGetCharacterKey(actor, session, out var characterKey))
        {
            Popup(actor, "Unable to identify character.");
            return;
        }

        if (!Guid.TryParse(args.PolicyId, out var policyId) ||
            !TryReadPolicy(characterKey, policyId, out var policy))
        {
            Popup(actor, "Policy not found.");
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        PruneStaleLiveCopies(policy);
        if (LiveCurrentCopyExists(policy))
        {
            Popup(actor, "That item already exists in this timeline.");
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!TryReadItemYaml(policy, out var yaml))
        {
            Popup(actor, "The saved item image is missing.");
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!TryLoadItem(yaml, policy, out var loaded))
        {
            Popup(actor, "The saved item image failed to resolve.");
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!_bank.TryGetBalance(actor, out var balance))
        {
            QueueDel(loaded.Value.Owner);
            Popup(actor, "Unable to read bank balance.");
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        var claimCost = CalculateClaimCost(ent.Comp, policy.Value, balance);
        if (!_bank.TryBankWithdraw(actor, claimCost))
        {
            QueueDel(loaded.Value.Owner);
            Popup(actor, $"Insufficient funds. Claim cost: {claimCost} spesos.");
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        policy.Generation++;
        policy.ClaimCost = claimCost;
        var insured = EnsureComp<InsuredItemComponent>(loaded.Value.Owner);
        insured.PolicyId = policy.PolicyId.ToString("N");
        insured.Generation = policy.Generation;
        insured.OwnerUserId = policy.OwnerUserId;
        insured.OwnerCharacterKey = GetPolicyStorageKey(policy);
        Dirty(loaded.Value.Owner, insured);

        if (!TryWritePolicy(policy, yaml))
        {
            _bank.TryBankDeposit(actor, claimCost);
            QueueDel(loaded.Value.Owner);
            Popup(actor, "The policy failed to update.");
            UpdateUi(ent.Owner, ent.Comp, actor);
            return;
        }

        _transform.SetCoordinates(loaded.Value.Owner, loaded.Value.Comp, Transform(ent.Owner).Coordinates);
        _transform.AttachToGridOrMap(loaded.Value.Owner, loaded.Value.Comp);
        _transform.DropNextTo(loaded.Value.Owner, ent.Owner);

        Popup(actor, $"Claimed {policy.Name} for {claimCost} spesos.");
        UpdateUi(ent.Owner, ent.Comp, actor);
    }

    public bool ShouldPruneLoadedInsuredItem(EntityUid uid, InsuredItemComponent insured)
    {
        var ownerKey = GetPolicyStorageKey(insured);
        if (string.IsNullOrEmpty(ownerKey) || !TryGetPolicyId(insured, out var policyId))
            return false;

        if (!TryReadPolicy(ownerKey, policyId, out var policy))
            return false;

        if (insured.Generation < policy.Generation)
            return true;

        return LiveCurrentCopyExists(policy, except: uid);
    }

    public void PruneLoadedInsuredItem(EntityUid uid, InsuredItemComponent insured)
    {
        if (!ShouldPruneLoadedInsuredItem(uid, insured))
            return;

        QueueDel(uid);
    }

    private void PruneStaleLiveCopies(ItemInsurancePolicyRecord policy)
    {
        var query = EntityQueryEnumerator<InsuredItemComponent>();
        while (query.MoveNext(out var uid, out var insured))
        {
            if (!PolicyMatches(insured, policy))
                continue;

            if (insured.Generation < policy.Generation)
                QueueDel(uid);
        }
    }

    private bool LiveCurrentCopyExists(ItemInsurancePolicyRecord policy, EntityUid? except = null)
    {
        var query = EntityQueryEnumerator<InsuredItemComponent>();
        while (query.MoveNext(out var uid, out var insured))
        {
            if (except != null && uid == except.Value)
                continue;

            if (PolicyMatches(insured, policy) && insured.Generation >= policy.Generation)
                return true;
        }

        return false;
    }

    private bool CanInsureItem(EntityUid item, [NotNullWhen(false)] out string? reason)
    {
        reason = null;

        if (HasComp<NotInsurableComponent>(item))
            reason = "That item cannot be insured.";
        else if (HasComp<ParadoxGeneratorComponent>(item))
            reason = "The generator refuses to insure itself.";
        else if (HasComp<MapGridComponent>(item) || HasComp<MapComponent>(item))
            reason = "Only items can be insured.";
        else if (HasComp<ActorComponent>(item) || HasComp<MindContainerComponent>(item))
            reason = "Living beings cannot be insured here.";

        return reason == null;
    }

    private int GetInsuredValue(EntityUid item)
    {
        var value = _pricing.GetPrice(item, includeContents: true, allowSideEffects: false);
        return Math.Max(0, (int)Math.Ceiling(value));
    }

    private static int CalculatePremium(ParadoxGeneratorComponent generator, int value, int balance)
    {
        var price = value * generator.PremiumItemValueMultiplier
                    + CalculateWealthTax(generator, balance) * generator.PremiumWealthTaxMultiplier;

        return Math.Clamp((int)Math.Ceiling(price), generator.MinPremium, generator.MaxPremium);
    }

    private static int CalculateClaimCost(ParadoxGeneratorComponent generator, int value, int balance)
    {
        var price = value * generator.ClaimItemValueMultiplier
                    + CalculateWealthTax(generator, balance) * generator.ClaimWealthTaxMultiplier;

        return Math.Clamp((int)Math.Ceiling(price), generator.MinClaim, generator.MaxClaim);
    }

    private static double CalculateWealthTax(ParadoxGeneratorComponent generator, int balance)
    {
        if (balance <= generator.WealthTaxThreshold)
            return balance * generator.WealthTaxBaseRate;

        return generator.WealthTaxThreshold * generator.WealthTaxBaseRate
               + (balance - generator.WealthTaxThreshold) * generator.WealthTaxExcessRate;
    }

    private static bool TryGetPolicyId(InsuredItemComponent insured, out Guid policyId)
    {
        return Guid.TryParse(insured.PolicyId, out policyId);
    }

    private static bool PolicyMatches(InsuredItemComponent insured, ItemInsurancePolicyRecord policy)
    {
        return TryGetPolicyId(insured, out var policyId) && policyId == policy.PolicyId;
    }

    private void RestoreInsuranceComponent(
        EntityUid item,
        bool hadInsurance,
        string previousPolicyId,
        string previousOwnerUserId,
        string previousOwnerCharacterKey,
        long previousGeneration)
    {
        if (!hadInsurance)
        {
            RemComp<InsuredItemComponent>(item);
            return;
        }

        var insured = EnsureComp<InsuredItemComponent>(item);
        insured.PolicyId = previousPolicyId;
        insured.OwnerUserId = previousOwnerUserId;
        insured.OwnerCharacterKey = previousOwnerCharacterKey;
        insured.Generation = previousGeneration;
        Dirty(item, insured);
    }

    private bool TrySerializeItem(EntityUid item, out string yaml)
    {
        yaml = string.Empty;

        try
        {
            using var writer = new StringWriter();
            if (!_mapLoader.TrySaveEntity(item, writer))
                return false;

            yaml = writer.ToString();
            return !string.IsNullOrWhiteSpace(yaml);
        }
        catch
        {
            return false;
        }
    }

    private bool TryLoadItem(string yaml, ItemInsurancePolicyRecord policy, [NotNullWhen(true)] out Entity<TransformComponent>? loaded)
    {
        loaded = null;

        try
        {
            using var reader = new StringReader(yaml);
            if (!_mapLoader.TryLoadEntity(reader, $"{policy.PolicyId:N}{EntityExtension}", out loaded))
                return false;

            return loaded != null;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateUi(EntityUid uid, ParadoxGeneratorComponent generator, EntityUid actor)
    {
        if (!TryGetSession(actor, out var session))
            return;

        if (!TryGetCharacterKey(actor, session, out var characterKey))
            return;

        var inserted = generator.ItemSlot.ContainerSlot?.ContainedEntity;
        var hasInserted = inserted != null;
        var insertedName = inserted != null ? Name(inserted.Value) : null;
        var value = inserted != null && CanInsureItem(inserted.Value, out _) ? GetInsuredValue(inserted.Value) : 0;
        _bank.TryGetBalance(actor, out var balance);
        var premium = inserted != null && value > 0 ? CalculatePremium(generator, value, balance) : 0;

        var listings = new List<InsuranceListingState>();
        foreach (var policy in ListPolicies(characterKey))
        {
            listings.Add(new InsuranceListingState(
                policy.PolicyId.ToString(),
                policy.Name,
                policy.Value,
                CalculateClaimCost(generator, policy.Value, balance),
                policy.Generation,
                LiveCurrentCopyExists(policy)));
        }

        _ui.SetUiState(uid, ParadoxGeneratorUiKey.Key, new ParadoxGeneratorBoundUserInterfaceState(
            balance,
            hasInserted,
            insertedName,
            value,
            premium,
            listings));
    }

    private bool TryGetSession(EntityUid actor, [NotNullWhen(true)] out ICommonSession? session)
    {
        return _player.TryGetSessionByEntity(actor, out session);
    }

    private bool TryGetCharacterKey(EntityUid actor, ICommonSession session, out string characterKey)
    {
        characterKey = string.Empty;

        if (!_mind.TryGetMind(actor, out _, out var mindComp) || mindComp.UserId == null)
            return false;

        characterKey = BuildCharacterKey(mindComp.UserId.Value, mindComp.CharacterName ?? session.Name);
        return true;
    }

    private void Popup(EntityUid actor, string message)
    {
        _popup.PopupCursor(message, actor);
    }

    private bool CanUseGenerator(EntityUid uid, EntityUid actor)
    {
        if (this.IsPowered(uid, EntityManager))
            return true;

        Popup(actor, "The paradox generator is unpowered.");
        return false;
    }

    private IEnumerable<ItemInsurancePolicyRecord> ListPolicies(string ownerKey)
    {
        var ud = _resource.UserData;
        var dir = GetOwnerDir(ownerKey);
        if (!ud.Exists(dir))
            yield break;

        foreach (var entry in ud.DirectoryEntries(dir))
        {
            if (!entry.EndsWith(PolicyExtension, StringComparison.OrdinalIgnoreCase))
                continue;

            var id = entry[..^PolicyExtension.Length];
            if (!Guid.TryParse(id, out var policyId))
                continue;

            if (TryReadPolicy(ownerKey, policyId, out var policy))
                yield return policy;
        }
    }

    private bool TryReadPolicy(string ownerKey, Guid policyId, [NotNullWhen(true)] out ItemInsurancePolicyRecord? policy)
    {
        policy = null;
        try
        {
            var path = GetPolicyPath(ownerKey, policyId);
            var ud = _resource.UserData;
            if (!ud.Exists(path))
                return false;

            using var stream = ud.OpenRead(path);
            policy = JsonSerializer.Deserialize<ItemInsurancePolicyRecord>(stream);
            return policy != null && policy.PolicyId == policyId && GetPolicyStorageKey(policy) == ownerKey;
        }
        catch
        {
            return false;
        }
    }

    private bool TryWritePolicy(ItemInsurancePolicyRecord policy, string yaml)
    {
        try
        {
            var ud = _resource.UserData;
            var ownerKey = GetPolicyStorageKey(policy);
            ud.CreateDir(GetOwnerDir(ownerKey));

            using (var stream = ud.OpenWrite(GetPolicyPath(ownerKey, policy.PolicyId)))
            {
                JsonSerializer.Serialize(stream, policy);
            }

            using (var stream = ud.OpenWrite(GetEntityPath(ownerKey, policy.PolicyId)))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(yaml);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryReadItemYaml(ItemInsurancePolicyRecord policy, out string yaml)
    {
        yaml = string.Empty;
        try
        {
            var path = GetEntityPath(GetPolicyStorageKey(policy), policy.PolicyId);
            var ud = _resource.UserData;
            if (!ud.Exists(path))
                return false;

            using var stream = ud.OpenRead(path);
            using var reader = new StreamReader(stream);
            yaml = reader.ReadToEnd();
            return !string.IsNullOrWhiteSpace(yaml);
        }
        catch
        {
            return false;
        }
    }

    private static string GetPolicyStorageKey(ItemInsurancePolicyRecord policy)
    {
        return !string.IsNullOrEmpty(policy.OwnerCharacterKey)
            ? policy.OwnerCharacterKey
            : policy.OwnerUserId;
    }

    private static string GetPolicyStorageKey(InsuredItemComponent insured)
    {
        return !string.IsNullOrEmpty(insured.OwnerCharacterKey)
            ? insured.OwnerCharacterKey
            : insured.OwnerUserId;
    }

    private static ResPath GetOwnerDir(string ownerKey)
    {
        return new ResPath($"{SaveRoot}/{Sanitize(ownerKey)}");
    }

    private static ResPath GetPolicyPath(string ownerKey, Guid policyId)
    {
        return new ResPath($"{SaveRoot}/{Sanitize(ownerKey)}/{policyId:N}{PolicyExtension}");
    }

    private static ResPath GetEntityPath(string ownerKey, Guid policyId)
    {
        return new ResPath($"{SaveRoot}/{Sanitize(ownerKey)}/{policyId:N}{EntityExtension}");
    }

    private static string BuildCharacterKey(NetUserId userId, string characterName)
    {
        var safeName = characterName.Trim();
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "unknown";

        return $"{userId.UserId:N}_{safeName}";
    }

    private static string Sanitize(string value)
    {
        return string.Concat(value.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));
    }

    private sealed class ItemInsurancePolicyRecord
    {
        public Guid PolicyId { get; set; }
        public string OwnerUserId { get; set; } = string.Empty;
        public string OwnerCharacterKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Prototype { get; set; } = string.Empty;
        public int Value { get; set; }
        public int Premium { get; set; }
        public int ClaimCost { get; set; }
        public long Generation { get; set; }
        public DateTime InsuredAt { get; set; }
    }
}
