using Content.Server.Carrying;
using Content.Server.Body.Components;
using Content.Server.Medical.SuitSensors;
using Content.Server._Starlight.NullSpace;
using Content.Shared._Common.Consent;
using Content.Shared._Starlight.NullSpace;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Carrying;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.FloofStation;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.FloofStation;

public sealed class VoreSystem : EntitySystem
{
    [Dependency] private readonly SharedConsentSystem _consent = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly CarryingSystem _carrying = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SuitSensorSystem _suitSensors = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly NullSpacePhaseSystem _phase = default!;

    public static readonly ProtoId<ConsentTogglePrototype> PredConsent = "PredVore";
    public static readonly ProtoId<ConsentTogglePrototype> PreyConsent = "PreyVore";
    public static readonly ProtoId<ConsentTogglePrototype> DigestConsent = "Digestable";

    private readonly HashSet<EntityUid> _pendingConsentUpdates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConsentComponent, ComponentStartup>(OnConsentStartup);
        SubscribeLocalEvent<ConsentComponent, EntityConsentToggleUpdatedEvent>(OnConsentUpdated);

        SubscribeLocalEvent<VoreComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<VoreComponent, OnVoreDoAfter>(OnVoreDoAfter);
        SubscribeLocalEvent<VoreComponent, BeingGibbedEvent>(OnGibbedRemoveContent);
        SubscribeLocalEvent<VoreComponent, DestructionEventArgs>(OnDestroyedRemoveContent);
        SubscribeLocalEvent<VoreComponent, PolymorphedEvent>(OnPolymorphedTransferContent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var uid in _pendingConsentUpdates)
        {
            if (HasComp<ConsentComponent>(uid))
                ApplyVoreConsent(uid);
        }

        _pendingConsentUpdates.Clear();
    }

    private void OnConsentUpdated(EntityUid uid, ConsentComponent comp, EntityConsentToggleUpdatedEvent args)
    {
        if (args.ConsentToggleProtoId != PredConsent &&
            args.ConsentToggleProtoId != PreyConsent &&
            args.ConsentToggleProtoId != DigestConsent)
            return;

        _pendingConsentUpdates.Add(uid);
    }

    private void OnConsentStartup(EntityUid uid, ConsentComponent comp, ComponentStartup args)
    {
        _pendingConsentUpdates.Add(uid);
    }

    private void ApplyVoreConsent(EntityUid uid)
    {
        var hasPred = _consent.HasConsent(uid, PredConsent);
        var hasPrey = _consent.HasConsent(uid, PreyConsent);

        if (!hasPrey && IsInVoreContainer(uid) && _container.TryGetContainingContainer(uid, out var container))
            _container.Remove(uid, container);

        if (hasPred || hasPrey)
            EnsureComp<VoreComponent>(uid);
        else
            RemComp<VoreComponent>(uid);

        if (hasPred)
            EnsureComp<DigestComponent>(uid);
        else
            RemComp<DigestComponent>(uid);
    }

    private void OnGetVerbs(EntityUid uid, VoreComponent comp, GetVerbsEvent<Verb> args)
    {
        if (!_cfg.GetCVar(VoreCVars.VoreEnabled))
            return;

        BuildPhaseNomVerb(args);

        if (!args.CanInteract || !args.CanAccess)
            return;

        BuildVoreContainerVerbs(uid, comp, args);
    }

    private void BuildPhaseNomVerb(GetVerbsEvent<Verb> args)
    {
        var user = args.User;
        var target = args.Target;

        if (!IsPhaseNomable(user, target))
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("vore-devour"),
            Category = VoreVerbCategory.VoreGeneral,
            Act = () => TryVore(user, target, true)
        });
    }

    private void BuildVoreContainerVerbs(EntityUid uid, VoreComponent comp, GetVerbsEvent<Verb> args)
    {
        var user = args.User;
        var target = args.Target;

        if (user == target)
        {
            var container = _container.EnsureContainer<Container>(target, comp.ContainerId);
            if (container.ContainedEntities.Count > 0)
            {
                args.Verbs.Add(new Verb
                {
                    Text = Loc.GetString("vore-release-all"),
                    Category = VoreVerbCategory.VoreGeneral,
                    Act = () => TryReleasePrey(target, comp)
                });
            }

            return;
        }

        if (IsDevourable(user, target))
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("vore-devour"),
                Category = VoreVerbCategory.VoreGeneral,
            Act = () => TryVore(user, target)
            });
        }

        if (IsDevourable(target, user))
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("vore-insert-self"),
                Category = VoreVerbCategory.VoreGeneral,
                Act = () => TryVore(target, user)
            });
        }

        EntityUid? carried = null;
        if (TryComp<CarryingComponent>(user, out var carrying) && carrying.Carried != default)
            carried = carrying.Carried;
        else if (TryComp<PullerComponent>(user, out var puller) && puller.Pulling is { } pulling)
            carried = pulling;

        if (carried is not { } prey || prey == target || !IsDevourable(target, prey))
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("vore-insert-other", ("entity", prey)),
            Category = VoreVerbCategory.VoreGeneral,
            Act = () => TryVore(target, prey)
        });
    }

    private void TryVore(EntityUid pred, EntityUid prey, bool phaseNom = false)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, pred, 5f, new OnVoreDoAfter(phaseNom: phaseNom), pred, target: prey, used: pred)
        {
            BreakOnMove = true,
            BreakOnDamage = !phaseNom,
            BreakOnWeightlessMove = false,
            RequireCanInteract = !phaseNom,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        var popup = phaseNom
            ? Loc.GetString("vore-attempt-phasenom", ("prey", prey))
            : Loc.GetString("vore-attempt-devour", ("entity", pred), ("prey", prey));

        _popup.PopupEntity(popup, pred, pred);
        _popup.PopupEntity(popup, prey, prey, PopupType.LargeCaution);
    }

    private void OnVoreDoAfter(EntityUid uid, VoreComponent comp, OnVoreDoAfter args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } prey)
            return;

        var pred = uid;
        var container = _container.EnsureContainer<Container>(pred, comp.ContainerId);

        var count = 0;
        foreach (var ent in container.ContainedEntities)
        {
            if (HasComp<BodyComponent>(ent))
                count++;
        }

        if (count >= args.MaxPrey)
        {
            _popup.PopupEntity(Loc.GetString("vore-too-full"), pred, pred);
            return;
        }

        if (comp.SoundDevour != null)
        {
            if (_players.TryGetSessionByEntity(pred, out var predSession))
                _audio.PlayEntity(comp.SoundDevour, predSession, pred);
            if (_players.TryGetSessionByEntity(prey, out var preySession))
                _audio.PlayEntity(comp.SoundDevour, preySession, pred);
        }

        if (args.PhaseNom && HasComp<NullSpaceComponent>(pred))
        {
            var (position, rotation) = _transform.GetWorldPositionRotation(prey);
            _transform.SetWorldPositionRotation(pred, position, rotation);
            _phase.Phase(pred);
        }

        EnsureEntityFree(pred, prey, comp);
        _container.Insert(prey, container);
    }

    private void EnsureEntityFree(EntityUid pred, EntityUid prey, VoreComponent comp)
    {
        if (_container.TryGetContainingContainer(prey, out var currentContainer) &&
            currentContainer.ID != comp.ContainerId)
        {
            _container.Remove(prey, currentContainer);
        }

        if (TryComp<CarryingComponent>(pred, out var predCarrying) && predCarrying.Carried == prey)
            _carrying.DropCarried(pred, prey);

        if (TryComp<CarryingComponent>(prey, out var preyCarrying) && preyCarrying.Carried == pred)
            _carrying.DropCarried(prey, pred);

        if (TryComp<BeingCarriedComponent>(prey, out var preyBeingCarried) && preyBeingCarried.Carrier != pred)
            _carrying.DropCarried(preyBeingCarried.Carrier, prey);
    }

    public void TryReleasePrey(EntityUid pred, VoreComponent? comp = null)
    {
        if (!Resolve(pred, ref comp))
            return;

        var container = _container.EnsureContainer<Container>(pred, comp.ContainerId);
        var preyList = new List<EntityUid>(container.ContainedEntities);

        foreach (var prey in preyList)
        {
            _container.Remove(prey, container);
            _popup.PopupEntity(Loc.GetString("vore-released-self"), prey, prey);
        }

        _popup.PopupEntity(Loc.GetString("vore-release-all-finished"), pred, pred);
    }

    public void EmptyVoreContainer(EntityUid pred, VoreComponent comp)
    {
        if (_container.TryGetContainer(pred, comp.ContainerId, out var container))
            _container.EmptyContainer(container);
    }

    private void OnGibbedRemoveContent(EntityUid uid, VoreComponent comp, ref BeingGibbedEvent args)
    {
        TryReleasePrey(uid, comp);
    }

    private void OnDestroyedRemoveContent(EntityUid uid, VoreComponent comp, DestructionEventArgs args)
    {
        TryReleasePrey(uid, comp);
    }

    private void OnPolymorphedTransferContent(EntityUid uid, VoreComponent comp, PolymorphedEvent args)
    {
        TryReleasePrey(uid, comp);
    }

    public bool IsInVoreContainer(EntityUid uid)
    {
        return _container.TryGetContainingContainer(uid, out var container) &&
               TryComp<VoreComponent>(container.Owner, out var predComp) &&
               container.ID == predComp.ContainerId;
    }

    private bool IsDevourable(EntityUid pred, EntityUid prey)
    {
        if (pred == prey)
            return false;
        if (!_players.TryGetSessionByEntity(pred, out _) || !_players.TryGetSessionByEntity(prey, out _))
            return false;
        if (!HasComp<BodyComponent>(pred) || !HasComp<BodyComponent>(prey))
            return false;
        if (!IsValidVoreInteraction(pred, prey))
            return false;
        if (!_consent.HasConsent(pred, PredConsent) || !_consent.HasConsent(prey, PreyConsent))
            return false;
        if (_mobState.IsDead(prey) || _mobState.IsCritical(prey))
            return false;

        return true;
    }

    private bool IsPhaseNomable(EntityUid pred, EntityUid prey)
    {
        if (!HasComp<NullSpaceComponent>(pred))
            return false;
        if (HasComp<NullSpaceComponent>(prey))
            return false;
        if (HasComp<DevouredComponent>(pred))
            return false;

        return IsDevourable(pred, prey);
    }

    private bool IsValidVoreInteraction(EntityUid pred, EntityUid prey)
    {
        var predInVore = IsInVoreContainer(pred);
        var preyInVore = IsInVoreContainer(prey);

        if (predInVore != preyInVore)
            return false;

        if (!predInVore)
            return true;

        return _container.TryGetContainingContainer(pred, out var predContainer) &&
               _container.TryGetContainingContainer(prey, out var preyContainer) &&
               predContainer.Owner == preyContainer.Owner;
    }
}
