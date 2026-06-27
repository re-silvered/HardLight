using Content.Server.Atmos.Components;
using Content.Server.Medical.SuitSensors;
using Content.Server.Radiation.Components;
using Content.Shared._Shitmed.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.FloofStation;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Temperature.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Server.FloofStation;

public sealed class DevouredSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SuitSensorSystem _suitSensors = default!;

    private readonly HashSet<EntityUid> _pendingImmunityUpdates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoreComponent, EntInsertedIntoContainerMessage>(OnPreyInserted);
        SubscribeLocalEvent<VoreComponent, EntRemovedFromContainerMessage>(OnPreyRemoved);

        SubscribeLocalEvent<DevouredComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DevouredComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<DevouredComponent, MobStateChangedEvent>(OnPreyMobStateChanged);
        SubscribeLocalEvent<DevouredComponent, MoveInputEvent>(OnRelayMovement);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var uid in _pendingImmunityUpdates)
            RemoveStomachImmunities(uid);

        _pendingImmunityUpdates.Clear();
    }

    private void OnPreyInserted(EntityUid uid, VoreComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != comp.ContainerId)
            return;

        EnsureComp<DevouredComponent>(args.Entity);
    }

    private void OnPreyRemoved(EntityUid uid, VoreComponent comp, EntRemovedFromContainerMessage args)
    {
        if (TryComp<DevouredComponent>(args.Entity, out _))
            _pendingImmunityUpdates.Add(args.Entity);
    }

    private void OnStartup(EntityUid uid, DevouredComponent comp, ComponentStartup args)
    {
        ApplyStomachImmunities(uid);
    }

    private void OnGetVerbs(EntityUid uid, DevouredComponent comp, GetVerbsEvent<Verb> args)
    {
        if (!_container.TryGetContainingContainer(uid, out var container))
            return;
        if (args.User != args.Target)
            return;

        var pred = container.Owner;
        var prey = uid;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("vore-struggle-free"),
            Category = VoreVerbCategory.VoreGeneral,
            Act = () =>
            {
                _popup.PopupEntity(Loc.GetString("vore-struggle-free-self"), prey, prey);
                _popup.PopupEntity(Loc.GetString("vore-struggle-free-pred"), pred, pred);
                _container.Remove(prey, container);
            }
        });
    }

    private void OnPreyMobStateChanged(EntityUid uid, DevouredComponent comp, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead && args.NewMobState != MobState.Critical)
            return;

        var safety = 0;
        while (_container.TryGetContainingContainer(uid, out var container) &&
               TryComp<VoreComponent>(container.Owner, out var vore) &&
               container.ID == vore.ContainerId)
        {
            if (++safety > 10)
                break;
            if (!_container.Remove(uid, container))
                break;
        }
    }

    private void OnRelayMovement(EntityUid uid, DevouredComponent comp, ref MoveInputEvent args)
    {
        if (!IsInVoreContainer(uid))
            return;

        args.Entity.Comp.HeldMoveButtons = default;
    }

    private bool IsInVoreContainer(EntityUid uid)
    {
        return _container.TryGetContainingContainer(uid, out var container) &&
               TryComp<VoreComponent>(container.Owner, out var comp) &&
               container.ID == comp.ContainerId;
    }

    private void ApplyStomachImmunities(EntityUid prey)
    {
        if (!IsInVoreContainer(prey))
            return;
        if (!TryComp<DevouredComponent>(prey, out var tracker))
            return;

        if (!HasComp<PressureImmunityComponent>(prey))
        {
            EnsureComp<PressureImmunityComponent>(prey);
            tracker.AddedPressure = true;
        }

        if (!HasComp<BreathingImmunityComponent>(prey))
        {
            EnsureComp<BreathingImmunityComponent>(prey);
            tracker.AddedBreathing = true;
        }

        if (!HasComp<TemperatureImmunityComponent>(prey))
        {
            EnsureComp<TemperatureImmunityComponent>(prey);
            tracker.AddedTemperature = true;
        }

        if (!HasComp<RadiationProtectionComponent>(prey))
        {
            EnsureComp<RadiationProtectionComponent>(prey);
            tracker.AddedRadiation = true;
        }

        _suitSensors.SetAllSensors(prey, SuitSensorMode.SensorOff);
    }

    private void RemoveStomachImmunities(EntityUid prey)
    {
        if (IsInVoreContainer(prey))
            return;
        if (!TryComp<DevouredComponent>(prey, out var tracker))
            return;

        if (tracker.AddedPressure)
            RemComp<PressureImmunityComponent>(prey);
        if (tracker.AddedBreathing)
            RemComp<BreathingImmunityComponent>(prey);
        if (tracker.AddedTemperature)
            RemComp<TemperatureImmunityComponent>(prey);
        if (tracker.AddedRadiation)
            RemComp<RadiationProtectionComponent>(prey);

        _suitSensors.SetAllSensors(prey, SuitSensorMode.SensorCords);
        RemComp<DevouredComponent>(prey);
    }
}
