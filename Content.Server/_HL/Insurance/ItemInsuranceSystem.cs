using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Server._HL.RoundPersistence.SaveBans;
using Content.Server._NF.Bank;
using Content.Server.Cargo.Systems;
using Content.Server.Nutrition.Components;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared._HL.Insurance;
using Content.Shared._HL.Insurance.Components;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Storage;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server._HL.Insurance;

public sealed class ItemInsuranceSystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceManager _resource = default!;
    [Dependency] private readonly SaveBanApi _saveBanApi = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public const string SaveRoot = "/item_insurance";
    private const string PolicyExtension = ".json";
    private const string EntityExtension = ".yml";
    private const string ComponentTypeKey = "type";
    private const string ContainerManagerComponentName = "ContainerContainer";
    private const string EntitiesKey = "entities";
    private const string StorageComponentName = "Storage";
    private const string TransformComponentName = "Transform";

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
        SubscribeLocalEvent<ParadoxGeneratorComponent, ParadoxGeneratorUninsureMessage>(OnUninsure);
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
        SendUiState(ent.Owner, ent.Comp, args.Actor);
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
            SendUiState(ent.Owner, ent.Comp, actor);
    }

    private void OnRefresh(Entity<ParadoxGeneratorComponent> ent, ref ParadoxGeneratorRefreshMessage args)
    {
        if (!CanUseGenerator(ent.Owner, args.Actor))
            return;

        SendUiState(ent.Owner, ent.Comp, args.Actor);
    }

    private void OnInsure(Entity<ParadoxGeneratorComponent> ent, ref ParadoxGeneratorInsureMessage args)
    {
        var actor = args.Actor;
        if (!CanUseGenerator(ent.Owner, actor))
            return;

        if (!TryGetSession(actor, out var session))
        {
            Popup(actor, "paradox-generator-popup-unavailable");
            return;
        }

        if (!TryGetCharacterKey(actor, session, out var characterKey))
        {
            Popup(actor, "paradox-generator-popup-unavailable");
            return;
        }

        if (ent.Comp.ItemSlot.ContainerSlot?.ContainedEntity is not { } item)
        {
            Popup(actor, "paradox-generator-insure-tooltip-no-item");
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!CanInsureItem(item, out var reason))
        {
            Popup(actor, reason);
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!_bank.TryGetBalance(actor, out var balance))
        {
            Popup(actor, "paradox-generator-popup-unavailable");
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        var value = GetInsuredValue(item);
        var premium = CalculatePremium(ent.Comp, value, balance);
        var claimCost = CalculateClaimCost(ent.Comp, value, balance);

        if (balance < premium)
        {
            Popup(actor, "paradox-generator-popup-insufficient-premium", ("premium", premium));
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

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
            Popup(actor, "paradox-generator-popup-serialize-failed");
            SendUiState(ent.Owner, ent.Comp, actor);
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
            Popup(actor, "paradox-generator-popup-insufficient-premium", ("premium", premium));
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!TryWritePolicy(policy, yaml))
        {
            _bank.TryBankDeposit(actor, premium);
            RestoreInsuranceComponent(item, hadInsurance, previousPolicyId, previousOwnerUserId, previousOwnerCharacterKey, previousGeneration);
            Popup(actor, "paradox-generator-popup-policy-write-failed");
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        Popup(actor, "paradox-generator-popup-insured", ("item", policy.Name), ("premium", premium));
        SendUiState(ent.Owner, ent.Comp, actor);
    }

    private void OnClaim(Entity<ParadoxGeneratorComponent> ent, ref ParadoxGeneratorClaimMessage args)
    {
        var actor = args.Actor;
        if (!CanUseGenerator(ent.Owner, actor))
            return;

        if (!TryGetSession(actor, out var session))
        {
            Popup(actor, "paradox-generator-popup-unavailable");
            return;
        }

        if (!TryGetCharacterKey(actor, session, out var characterKey))
        {
            Popup(actor, "paradox-generator-popup-unavailable");
            return;
        }

        if (!Guid.TryParse(args.PolicyId, out var policyId) ||
            !TryReadPolicy(characterKey, policyId, out var policy))
        {
            Popup(actor, "paradox-generator-popup-policy-not-found");
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        if (PolicyNoLongerInsurable(policy))
        {
            Popup(actor, "paradox-generator-claim-tooltip-not-insurable");
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        PruneStaleLiveCopies(policy);
        if (LiveCurrentCopyExists(policy))
        {
            Popup(actor, "paradox-generator-popup-live-copy");
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!TryReadItemYaml(policy, out var yaml))
        {
            Popup(actor, "paradox-generator-popup-insured-record-missing");
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!_bank.TryGetBalance(actor, out var balance))
        {
            Popup(actor, "paradox-generator-popup-unavailable");
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        var claimCost = CalculateClaimCost(ent.Comp, policy.Value, balance);
        if (!_bank.TryBankWithdraw(actor, claimCost))
        {
            Popup(actor, "paradox-generator-popup-insufficient-claim", ("claimCost", claimCost));
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!TryLoadItem(yaml, policy, out var loaded))
        {
            _bank.TryBankDeposit(actor, claimCost);
            Popup(actor, "paradox-generator-popup-insured-record-load-failed");
            SendUiState(ent.Owner, ent.Comp, actor);
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
        DeleteLoadedStorageContents(loaded.Value.Owner);

        if (!TryWritePolicy(policy, yaml))
        {
            _bank.TryBankDeposit(actor, claimCost);
            QueueDel(loaded.Value.Owner);
            Popup(actor, "paradox-generator-popup-policy-update-failed");
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        _transform.SetCoordinates(loaded.Value.Owner, loaded.Value.Comp, Transform(ent.Owner).Coordinates);
        _transform.AttachToGridOrMap(loaded.Value.Owner, loaded.Value.Comp);
        _transform.DropNextTo(loaded.Value.Owner, ent.Owner);

        Popup(actor, "paradox-generator-popup-claimed", ("item", policy.Name), ("claimCost", claimCost));
        SendUiState(ent.Owner, ent.Comp, actor);
    }

    private void OnUninsure(Entity<ParadoxGeneratorComponent> ent, ref ParadoxGeneratorUninsureMessage args)
    {
        var actor = args.Actor;
        if (!CanUseGenerator(ent.Owner, actor))
            return;

        if (!TryGetSession(actor, out var session))
        {
            Popup(actor, "paradox-generator-popup-unavailable");
            return;
        }

        if (!TryGetCharacterKey(actor, session, out var characterKey))
        {
            Popup(actor, "paradox-generator-popup-unavailable");
            return;
        }

        if (!Guid.TryParse(args.PolicyId, out var policyId) ||
            !TryReadPolicy(characterKey, policyId, out var policy))
        {
            Popup(actor, "paradox-generator-popup-policy-not-found");
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        if (!TryDeletePolicy(policy))
        {
            Popup(actor, "paradox-generator-popup-uninsure-failed");
            SendUiState(ent.Owner, ent.Comp, actor);
            return;
        }

        RemoveInsuranceMarkers(policy);
        Popup(actor, "paradox-generator-popup-uninsured", ("item", policy.Name));
        SendUiState(ent.Owner, ent.Comp, actor);
    }

    public bool ShouldPruneLoadedInsuredItem(EntityUid uid, InsuredItemComponent insured)
    {
        if (!TryGetPolicyForMarker(insured, out var policy))
            return false;

        if (insured.Generation < policy.Generation)
            return true;

        return LiveCurrentCopyExists(policy, except: uid);
    }

    public void PruneLoadedInsuredItem(EntityUid uid, InsuredItemComponent insured)
    {
        if (!TryGetPolicyForMarker(insured, out _))
        {
            RemComp<InsuredItemComponent>(uid);
            return;
        }

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

    private bool TryGetPolicyForMarker(
        InsuredItemComponent insured,
        [NotNullWhen(true)] out ItemInsurancePolicyRecord? policy)
    {
        policy = null;
        var ownerKey = GetPolicyStorageKey(insured);
        return !string.IsNullOrEmpty(ownerKey) &&
               TryGetPolicyId(insured, out var policyId) &&
               TryReadPolicy(ownerKey, policyId, out policy);
    }

    private void RemoveInsuranceMarkers(ItemInsurancePolicyRecord policy)
    {
        var toRemove = new List<EntityUid>();
        var ownerKey = GetPolicyStorageKey(policy);
        var query = EntityQueryEnumerator<InsuredItemComponent>();
        while (query.MoveNext(out var uid, out var insured))
        {
            if (GetPolicyStorageKey(insured) == ownerKey && PolicyMatches(insured, policy))
                toRemove.Add(uid);
        }

        foreach (var uid in toRemove)
            RemComp<InsuredItemComponent>(uid);
    }

    private bool CanInsureItem(EntityUid item, [NotNullWhen(false)] out string? reason)
    {
        reason = item switch
        {
            // Already insured
            _ when HasComp<InsuredItemComponent>(item)
                => "paradox-generator-popup-already-insured",

            // Uninsurable by condition
            _ when HasTotalSaveBan(item) ||
                   HasComp<NotInsurableComponent>(item) ||
                   HasComp<FoodComponent>(item) ||
                   HasComp<DrinkComponent>(item) ||
                   HasComp<PillComponent>(item) ||
                   HasComp<SmokableComponent>(item) ||
                   HasComp<ActorComponent>(item) ||
                   HasComp<MindContainerComponent>(item)
                => "paradox-generator-popup-not-insurable",

            // Default: it is insurable
            _ => null,
        };

        return reason == null;
    }

    private bool PolicyNoLongerInsurable(ItemInsurancePolicyRecord policy)
    {
        if (string.IsNullOrEmpty(policy.Prototype))
            return false;

        if (IsTotalSaveBannedPrototype(policy.Prototype))
            return true;

        return _prototype.TryIndex<EntityPrototype>(policy.Prototype, out var prototype) &&
               PrototypeHasAnyInsuranceBannedComponent(prototype);
    }

    private bool IsTotalSaveBannedPrototype(string prototypeId)
    {
        EntityPrototype? prototype = null;
        foreach (var ban in _saveBanApi.Bans)
        {
            switch (ban.BannedFlag)
            {
                case SaveBanStore.SaveBanFlag.SaveBanFlagByEntity entityFlag:
                    if (entityFlag.Prototype == prototypeId)
                        return true;
                    break;

                case SaveBanStore.SaveBanFlag.SaveBanFlagByComponent componentFlag:
                    if (prototype == null && !_prototype.TryIndex<EntityPrototype>(prototypeId, out prototype))
                        break;

                    if (prototype.Components.ContainsKey(componentFlag.Name))
                        return true;
                    break;
            }
        }

        return false;
    }

    private bool PrototypeHasAnyInsuranceBannedComponent(EntityPrototype prototype)
    {
        return PrototypeHasComponent<NotInsurableComponent>(prototype) ||
               PrototypeHasComponent<FoodComponent>(prototype) ||
               PrototypeHasComponent<DrinkComponent>(prototype) ||
               PrototypeHasComponent<PillComponent>(prototype) ||
               PrototypeHasComponent<SmokableComponent>(prototype) ||
               PrototypeHasComponent<ActorComponent>(prototype) ||
               PrototypeHasComponent<MindContainerComponent>(prototype);
    }

    private bool PrototypeHasComponent<T>(EntityPrototype prototype) where T : Component
    {
        return prototype.Components.ContainsKey(_componentFactory.GetComponentName(typeof(T)));
    }

    private bool HasTotalSaveBan(EntityUid item)
    {
        var restriction = _saveBanApi.CheckForRestrictions(item);
        return restriction switch
        {
            SaveBanApi.SaveBanResult.IsSaveRestricted =>
                true,
            SaveBanApi.SaveBanResult.ContainsSaveRestricted contains =>
                SaveBanApi.FlattenRestrictions(contains)
                    .Any(_ => true),
            _ => false,
        };
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
            var options = SerializationOptions.Default with
            {
                MissingEntityBehaviour = MissingEntityBehaviour.Ignore,
                ErrorOnOrphan = false,
                LogAutoInclude = null,
                Category = FileCategory.Entity,
            };

            var (node, category) = _mapLoader.SerializeEntitiesRecursive([item], options);
            if (category != FileCategory.Entity)
                return false;

            StripStorageContentsFromSnapshot(node);
            yaml = WriteNodeToString(node);
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

    private static void StripStorageContentsFromSnapshot(MappingDataNode node)
    {
        var entitiesById = GetSerializedEntities(node);
        if (entitiesById.Count == 0)
            return;

        var childrenByParent = new Dictionary<string, List<string>>();
        var storageIds = new HashSet<string>();

        foreach (var (id, entity) in entitiesById)
        {
            if (TryGetComponents(entity, out var components) &&
                HasComponentNode(components, StorageComponentName))
            {
                storageIds.Add(id);
            }

            var parentId = GetSerializedParentId(entity);
            if (parentId == null)
                continue;

            if (!childrenByParent.TryGetValue(parentId, out var children))
            {
                children = new List<string>();
                childrenByParent[parentId] = children;
            }

            children.Add(id);
        }

        var removeIds = new HashSet<string>();
        foreach (var storageId in storageIds)
        {
            CollectSerializedDescendants(storageId, childrenByParent, removeIds);
        }

        RemoveSerializedEntities(node, removeIds);
        ClearSerializedStorageState(entitiesById, removeIds);
        UpdateSerializedEntityCount(node, entitiesById.Count - removeIds.Count);
    }

    private static Dictionary<string, MappingDataNode> GetSerializedEntities(MappingDataNode node)
    {
        var entitiesById = new Dictionary<string, MappingDataNode>();
        if (!node.TryGet(EntitiesKey, out SequenceDataNode? groups))
            return entitiesById;

        foreach (var groupNode in groups)
        {
            if (groupNode is not MappingDataNode group ||
                !group.TryGet(EntitiesKey, out SequenceDataNode? entities))
                continue;

            foreach (var entityNode in entities)
            {
                if (entityNode is not MappingDataNode entity ||
                    !TryGetSerializedUid(entity, out var id))
                    continue;

                entitiesById[id] = entity;
            }
        }

        return entitiesById;
    }

    private static void CollectSerializedDescendants(
        string parentId,
        IReadOnlyDictionary<string, List<string>> childrenByParent,
        HashSet<string> removeIds)
    {
        if (!childrenByParent.TryGetValue(parentId, out var children))
            return;

        foreach (var child in children)
        {
            if (!removeIds.Add(child))
                continue;

            CollectSerializedDescendants(child, childrenByParent, removeIds);
        }
    }

    private static void RemoveSerializedEntities(MappingDataNode node, HashSet<string> removeIds)
    {
        if (removeIds.Count == 0)
            return;

        if (!node.TryGet(EntitiesKey, out SequenceDataNode? groups))
            return;

        for (var groupIndex = groups.Count - 1; groupIndex >= 0; groupIndex--)
        {
            if (groups[groupIndex] is not MappingDataNode group ||
                !group.TryGet(EntitiesKey, out SequenceDataNode? entities))
                continue;

            for (var entityIndex = entities.Count - 1; entityIndex >= 0; entityIndex--)
            {
                if (entities[entityIndex] is not MappingDataNode entity ||
                    !TryGetSerializedUid(entity, out var id) ||
                    !removeIds.Contains(id))
                    continue;

                entities.RemoveAt(entityIndex);
            }

            if (entities.Count == 0)
                groups.RemoveAt(groupIndex);
        }

        RemoveSerializedIds(node, "maps", removeIds);
        RemoveSerializedIds(node, "grids", removeIds);
        RemoveSerializedIds(node, "orphans", removeIds);
        RemoveSerializedIds(node, "nullspace", removeIds);
    }

    private static void RemoveSerializedIds(MappingDataNode node, string key, HashSet<string> removeIds)
    {
        if (!node.TryGet(key, out SequenceDataNode? ids))
            return;

        for (var i = ids.Count - 1; i >= 0; i--)
        {
            if (ids[i] is ValueDataNode id && removeIds.Contains(id.Value))
                ids.RemoveAt(i);
        }
    }

    private static void ClearSerializedStorageState(
        Dictionary<string, MappingDataNode> entitiesById,
        HashSet<string> removedIds)
    {
        foreach (var (id, entity) in entitiesById)
        {
            if (removedIds.Contains(id) || !TryGetComponents(entity, out var components))
                continue;

            var hasStorage = HasComponentNode(components, StorageComponentName);
            foreach (var componentNode in components)
            {
                if (componentNode is not MappingDataNode component)
                    continue;

                if (IsComponentNode(component, StorageComponentName))
                {
                    component.Remove("storedItems");
                    component.Remove("savedLocations");
                }
                else if (hasStorage && IsComponentNode(component, ContainerManagerComponentName))
                {
                    RemoveStorageContainer(component);
                }
            }
        }
    }

    private static void RemoveStorageContainer(MappingDataNode containerManager)
    {
        if (!containerManager.TryGet("containers", out MappingDataNode? containers))
            return;

        containers.Remove(StorageComponent.ContainerId);
        if (containers.Count == 0)
            containerManager.Remove("containers");
    }

    private static void UpdateSerializedEntityCount(MappingDataNode node, int entityCount)
    {
        if (node.TryGet("meta", out MappingDataNode? meta))
            meta["entityCount"] = new ValueDataNode(entityCount.ToString());
    }

    private static bool TryGetSerializedUid(MappingDataNode entity, [NotNullWhen(true)] out string? id)
    {
        id = null;
        if (!entity.TryGet("uid", out ValueDataNode? uid) || string.IsNullOrEmpty(uid.Value))
            return false;

        id = uid.Value;
        return true;
    }

    private static string? GetSerializedParentId(MappingDataNode entity)
    {
        if (!TryGetComponents(entity, out var components))
            return null;

        foreach (var componentNode in components)
        {
            if (componentNode is not MappingDataNode component ||
                !IsComponentNode(component, TransformComponentName) ||
                !component.TryGet("parent", out ValueDataNode? parent) ||
                string.IsNullOrEmpty(parent.Value))
                continue;

            return parent.Value;
        }

        return null;
    }

    private static bool TryGetComponents(MappingDataNode entity, [NotNullWhen(true)] out SequenceDataNode? components)
    {
        return entity.TryGet("components", out components);
    }

    private static bool HasComponentNode(SequenceDataNode components, string componentType)
    {
        foreach (var componentNode in components)
        {
            if (componentNode is MappingDataNode component && IsComponentNode(component, componentType))
                return true;
        }

        return false;
    }

    private static bool IsComponentNode(MappingDataNode component, string componentType)
    {
        return component.TryGet(ComponentTypeKey, out ValueDataNode? typeNode) &&
               typeNode.Value == componentType;
    }

    private static string WriteNodeToString(MappingDataNode node)
    {
        var document = new YamlDocument(node.ToYaml());
        using var writer = new StringWriter();
        var stream = new YamlStream { document };
        stream.Save(new YamlMappingFix(new Emitter(writer)), false);
        return writer.ToString();
    }

    private void DeleteLoadedStorageContents(EntityUid root)
    {
        var stack = new Stack<EntityUid>();
        stack.Push(root);

        while (stack.TryPop(out var uid))
        {
            if (!TryComp(uid, out TransformComponent? xform))
                continue;

            var childEnumerator = xform.ChildEnumerator;
            while (childEnumerator.MoveNext(out var child))
            {
                if (HasComp<StorageComponent>(uid))
                {
                    QueueDel(child);
                    continue;
                }

                stack.Push(child);
            }
        }
    }

    private void SendUiState(EntityUid uid, ParadoxGeneratorComponent generator, EntityUid actor)
    {
        if (!TryGetSession(actor, out var session))
            return;

        if (!TryGetCharacterKey(actor, session, out var characterKey))
            return;

        var inserted = generator.ItemSlot.ContainerSlot?.ContainedEntity;
        var hasInserted = inserted != null;
        var insertedName = inserted != null ? Name(inserted.Value) : null;
        var cannotInsureReason = inserted != null && !CanInsureItem(inserted.Value, out var reason) ? reason : null;
        var value = inserted != null ? GetInsuredValue(inserted.Value) : 0;
        _bank.TryGetBalance(actor, out var balance);
        var premium = inserted != null && value > 0 ? CalculatePremium(generator, value, balance) : 0;

        var listings = new List<InsuranceListingState>();
        foreach (var policy in ListPolicies(characterKey))
        {
            var cannotClaimReason = PolicyNoLongerInsurable(policy)
                ? "paradox-generator-claim-tooltip-not-insurable"
                : null;

            listings.Add(new InsuranceListingState(
                policy.PolicyId.ToString(),
                policy.Name,
                policy.Value,
                CalculateClaimCost(generator, policy.Value, balance),
                policy.Generation,
                cannotClaimReason == null && LiveCurrentCopyExists(policy),
                cannotClaimReason));
        }

        var state = new ParadoxGeneratorBoundUserInterfaceState(
            balance,
            hasInserted,
            insertedName,
            value,
            premium,
            cannotInsureReason,
            listings);

        RaiseNetworkEvent(new ParadoxGeneratorUiStateMessage(GetNetEntity(uid), state), session);
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

    private void Popup(EntityUid actor, string message, params (string, object)[] args)
    {
        _popup.PopupCursor(Loc.GetString(message, args), actor);
    }

    private bool CanUseGenerator(EntityUid uid, EntityUid actor)
    {
        if (this.IsPowered(uid, EntityManager))
            return true;

        Popup(actor, "paradox-generator-popup-unpowered");
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

    private bool TryDeletePolicy(ItemInsurancePolicyRecord policy)
    {
        try
        {
            var ud = _resource.UserData;
            var ownerKey = GetPolicyStorageKey(policy);
            var policyPath = GetPolicyPath(ownerKey, policy.PolicyId);
            var entityPath = GetEntityPath(ownerKey, policy.PolicyId);

            if (ud.Exists(policyPath))
                ud.Delete(policyPath);

            if (ud.Exists(entityPath))
                ud.Delete(entityPath);

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
