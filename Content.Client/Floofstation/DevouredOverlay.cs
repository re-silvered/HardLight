using System.Numerics;
using Content.Shared.FloofStation;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.FloofStation;

public sealed class DevouredOverlay : Overlay
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private static readonly Vector3 StomachWallColor = new(0.35f, 0.01f, 0.06f);
    private static readonly Color AmbientDimColor = new(0.0f, 0.0f, 0.0f, 0.65f);
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";

    private readonly ShaderInstance _stomachShader;

    public DevouredOverlay()
    {
        IoCManager.InjectDependencies(this);
        _stomachShader = _prototype.Index(CircleMaskShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var playerEntity = _player.LocalSession?.AttachedEntity;
        if (playerEntity == null)
            return false;
        if (!_entities.TryGetComponent(playerEntity, out EyeComponent? eyeComp) || args.Viewport.Eye != eyeComp.Eye)
            return false;

        return _entities.HasComponent<DevouredComponent>(playerEntity.Value);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;
        var viewport = args.WorldAABB;
        var viewWidth = args.ViewportBounds.Width;
        var time = (float) _timing.RealTime.TotalSeconds;
        const float digestionPreview = 1f;

        var outerRadius = (1.0f - digestionPreview * 0.6f) * viewWidth;
        var innerRadius = (0.25f - digestionPreview * 0.25f) * viewWidth;
        var outerCircleMaxRadius = outerRadius + 0.13f * viewWidth;
        var innerCircleMaxRadius = innerRadius + 0.03f * viewWidth;

        var pulsing = MathF.Cos(time * 0.5f - 1.5f) + 1f;
        _stomachShader.SetParameter("time", pulsing);
        _stomachShader.SetParameter("color", StomachWallColor);
        _stomachShader.SetParameter("darknessAlphaOuter", 0.99f);
        _stomachShader.SetParameter("outerCircleRadius", outerRadius);
        _stomachShader.SetParameter("outerCircleMaxRadius", outerCircleMaxRadius);
        _stomachShader.SetParameter("innerCircleRadius", innerRadius);
        _stomachShader.SetParameter("innerCircleMaxRadius", innerCircleMaxRadius);

        worldHandle.UseShader(_stomachShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
        worldHandle.DrawRect(viewport, AmbientDimColor);
    }
}
