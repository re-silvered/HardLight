using Content.Shared.FloofStation;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.FloofStation;

public sealed class DevouredBlindSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private DevouredOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevouredComponent, ComponentInit>(OnDevouredInit);
        SubscribeLocalEvent<DevouredComponent, ComponentShutdown>(OnDevouredShutdown);
        SubscribeLocalEvent<DevouredComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<DevouredComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new DevouredOverlay();
    }

    private void OnPlayerAttached(EntityUid uid, DevouredComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, DevouredComponent component, LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnDevouredInit(EntityUid uid, DevouredComponent component, ComponentInit args)
    {
        if (_player.LocalEntity == uid)
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnDevouredShutdown(EntityUid uid, DevouredComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid)
            _overlayMan.RemoveOverlay(_overlay);
    }
}

