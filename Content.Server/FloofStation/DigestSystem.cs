using System.Linq;
using Content.Server.Bed.Cryostorage;
using Content.Server.Mind;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Common.Consent;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FloofStation;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.PowerCell.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;

namespace Content.Server.FloofStation;

public sealed class DigestSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedConsentSystem _consent = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly CryostorageSystem _cryo = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DigestComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(EntityUid uid, DigestComponent comp, GetVerbsEvent<Verb> args)
    {
        if (!_cfg.GetCVar(VoreCVars.DigestionEnabled))
            return;
        if (args.User != uid)
            return;
        if (!args.CanInteract || !args.CanAccess)
            return;
        if (!_container.TryGetContainer(uid, "vore_container", out var container))
            return;
        if (container.ContainedEntities.Count == 0)
            return;

        foreach (var prey in container.ContainedEntities)
        {
            if (_consent.HasConsent(prey, "Digestable") && !comp.ActiveDigesting.Contains(prey))
            {
                args.Verbs.Add(new Verb
                {
                    Text = Loc.GetString("vore-digest", ("entity", prey)),
                    Category = VoreVerbCategory.VoreDigest,
                    Act = () => TryDigest(prey)
                });
            }
            else if (comp.ActiveDigesting.Contains(prey))
            {
                args.Verbs.Add(new Verb
                {
                    Text = Loc.GetString("vore-stop-digest", ("entity", prey)),
                    Category = VoreVerbCategory.VoreDigest,
                    Act = () => StopDigest(uid, prey)
                });
            }
        }
    }

    private void TryDigest(EntityUid prey)
    {
        if (!_container.TryGetContainingContainer(prey, out var container))
            return;
        var pred = container.Owner;
        if (!TryComp<DigestComponent>(pred, out var comp))
            return;

        _popup.PopupEntity(Loc.GetString("vore-digest-start", ("entity", pred)), pred, pred);
        _popup.PopupEntity(Loc.GetString("vore-digest-start", ("entity", pred)), prey, prey, PopupType.LargeCaution);

        comp.Health.TryAdd(prey, comp.Max);
        comp.ActiveDigesting.Add(prey);
        comp.Timer[prey] = 0f;
    }

    private void StopDigest(EntityUid pred, EntityUid prey)
    {
        if (!TryComp<DigestComponent>(pred, out var comp))
            return;

        comp.ActiveDigesting.Remove(prey);
        comp.Timer[prey] = 0f;

        _popup.PopupEntity(Loc.GetString("vore-digest-stop", ("entity", pred)), pred, pred);
        _popup.PopupEntity(Loc.GetString("vore-digest-stop", ("entity", pred)), prey, prey);
    }

    private void FinishDigest(EntityUid prey)
    {
        if (_container.TryGetContainingContainer(prey, out var container))
            _popup.PopupEntity(Loc.GetString("vore-digested-owner-1", ("entity", prey)), container.Owner, container.Owner);

        SendToCryo(prey);
    }

    private void SendToCryo(EntityUid prey)
    {
        var query = EntityQueryEnumerator<CryostorageComponent>();
        EntityUid? cryoUnit = null;
        while (query.MoveNext(out var uid, out _))
        {
            cryoUnit = uid;
            break;
        }

        if (cryoUnit == null)
        {
            QueueDel(prey);
            return;
        }

        var contained = EnsureComp<CryostorageContainedComponent>(prey);
        contained.Cryostorage = cryoUnit.Value;
        _mind.TryGetMind(prey, out _, out var mindComp);
        _cryo.HandleEnterCryostorage((prey, contained), mindComp?.UserId);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var preds = new List<(EntityUid Pred, DigestComponent Comp)>();
        var query = EntityQueryEnumerator<DigestComponent>();

        while (query.MoveNext(out var pred, out var comp))
            preds.Add((pred, comp));

        foreach (var (pred, comp) in preds)
        {
            var fullyDigest = new List<EntityUid>();

            foreach (var prey in comp.Health.Keys.ToList())
            {
                comp.Timer[prey] += frameTime;
                if (comp.Timer[prey] < 1f)
                    continue;
                comp.Timer[prey] -= 1f;

                if (!EntityManager.EntityExists(prey))
                {
                    fullyDigest.Add(prey);
                    continue;
                }

                if (comp.ActiveDigesting.Contains(prey))
                {
                    if (!_container.TryGetContainingContainer(prey, out var container) ||
                        container.ID != "vore_container" ||
                        !_consent.HasConsent(prey, "Digestable"))
                    {
                        comp.ActiveDigesting.Remove(prey);
                        comp.Timer[prey] = 0f;
                        continue;
                    }

                    comp.Health[prey] -= 0.5f;
                    ShowDigestPopup(prey, comp);

                    if (TryComp<HungerComponent>(container.Owner, out var hunger))
                        _hunger.ModifyHunger(container.Owner, 1, hunger);
                    else if (TryComp<BatteryComponent>(container.Owner, out var battery))
                        _battery.SetCharge(container.Owner, battery.CurrentCharge + 2f, battery);
                    else if (TryComp<PowerCellSlotComponent>(container.Owner, out var batterySlot) &&
                             _itemSlots.TryGetSlot(container.Owner, batterySlot.CellSlotId, out var itemSlot) &&
                             itemSlot.Item is { } cellUid &&
                             TryComp<BatteryComponent>(cellUid, out var batteryComp))
                    {
                        _battery.SetCharge(cellUid, batteryComp.CurrentCharge + 2f, batteryComp);
                    }

                    if (comp.Health[prey] <= 0)
                        fullyDigest.Add(prey);
                }
                else
                {
                    if (TryComp<HungerComponent>(prey, out var preyHunger))
                    {
                        if (_hunger.GetHunger(preyHunger) > 50 && comp.Health[prey] < comp.Max)
                        {
                            comp.Health[prey] += 0.1f;
                            _hunger.ModifyHunger(prey, -1f, preyHunger);
                        }
                    }
                    else if (TryComp<BatteryComponent>(prey, out var preyBattery))
                    {
                        if (preyBattery.CurrentCharge > preyBattery.MaxCharge * 0.5f && comp.Health[prey] < comp.Max)
                        {
                            comp.Health[prey] += 0.1f;
                            _battery.SetCharge(prey, preyBattery.CurrentCharge - 1f, preyBattery);
                        }
                    }
                    else if (TryComp<PowerCellSlotComponent>(prey, out var batterySlot) &&
                             _itemSlots.TryGetSlot(prey, batterySlot.CellSlotId, out var itemSlot) &&
                             itemSlot.Item is { } cellUid &&
                             TryComp<BatteryComponent>(cellUid, out var batteryComp) &&
                             batteryComp.CurrentCharge > batteryComp.MaxCharge * 0.5f &&
                             comp.Health[prey] < comp.Max)
                    {
                        comp.Health[prey] += 0.1f;
                        _battery.SetCharge(cellUid, batteryComp.CurrentCharge - 2f, batteryComp);
                    }
                }
            }

            foreach (var prey in fullyDigest)
            {
                comp.Health.Remove(prey);
                comp.Timer.Remove(prey);
                comp.ActiveDigesting.Remove(prey);
                comp.DigestPopupStage.Remove(prey);
                FinishDigest(prey);
            }
        }
    }

    private void ShowDigestPopup(EntityUid prey, DigestComponent comp)
    {
        var percent = comp.Health[prey] / comp.Max;
        var stage = 0;

        if (percent <= 0.10f)
            stage = 4;
        else if (percent <= 0.25f)
            stage = 3;
        else if (percent <= 0.50f)
            stage = 2;
        else if (percent <= 0.75f)
            stage = 1;

        if (stage == 0)
            return;
        if (comp.DigestPopupStage.TryGetValue(prey, out var lastStage) && lastStage >= stage)
            return;

        comp.DigestPopupStage[prey] = stage;

        var message = stage switch
        {
            1 => "vore-digest-stage-1",
            2 => "vore-digest-stage-2",
            3 => "vore-digest-stage-3",
            4 => "vore-digest-stage-4",
            _ => null
        };

        if (message != null)
            _popup.PopupEntity(Loc.GetString(message), prey, prey);
    }
}
