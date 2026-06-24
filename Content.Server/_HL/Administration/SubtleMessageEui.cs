using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Server.Popups;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Eui;
using Content.Shared.HL.Administration;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.HL.Administration;

public sealed class SubtleMessageEui : BaseEui
{
    private const float MinScale = 0.5f;
    private const float MaxScale = 3f;
    private const float MinCharactersPerSecond = 0.1f;
    private const float MaxCharactersPerSecond = 60f;
    private const float MinWaveSpeed = 0f;
    private const float MaxWaveSpeed = 30f;
    private const float MinWaveHeight = 0f;
    private const float MaxWaveHeight = 40f;
    private const float MinLingerTime = 0f;
    private const float MaxLingerTime = 30f;

    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    private readonly PopupSystem _popup;
    private readonly SharedTransformSystem _transform;
    private readonly NetUserId? _selectedPlayer;
    private readonly EntityCoordinates? _defaultCoordinates;

    public SubtleMessageEui()
    {
        IoCManager.InjectDependencies(this);
        _popup = _entityManager.System<PopupSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();
    }

    public SubtleMessageEui(ICommonSession? selectedPlayer, EntityCoordinates? defaultCoordinates) : this()
    {
        _selectedPlayer = selectedPlayer?.UserId;
        _defaultCoordinates = defaultCoordinates;
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        var players = _players.Sessions
            .OrderBy(session => session.Name)
            .Select(session => new SubtleMessagePlayer(session.UserId, session.Name))
            .ToList();

        var hasCoordinates = TryGetDefaultCoordinates(out var mapCoordinates);
        var hasUserCoordinates = TryGetUserCoordinates(out var userCoordinates);

        return new SubtleMessageEuiState(
            players,
            _selectedPlayer,
            hasCoordinates,
            mapCoordinates.Position.X,
            mapCoordinates.Position.Y,
            hasUserCoordinates,
            userCoordinates.Position.X,
            userCoordinates.Position.Y);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not SubtleMessageRequest request)
            return;

        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            Close();
            return;
        }

        request.PopupMessage = request.PopupMessage.Trim();
        request.ChatMessage = request.ChatMessage.Trim();

        if (request.PopupMessage.Length == 0)
            return;

        ClampStyle(request.Style);

        var targets = ResolveTargets(request);
        if (targets.Count == 0)
            return;

        var logTargets = new List<string>(targets.Count);
        foreach (var target in targets)
        {
            if (target.AttachedEntity is not { } attached || !_entityManager.EntityExists(attached))
                continue;

            SendMessage(target, request);
            logTargets.Add($"{target.Name} ({target.UserId})");
        }

        if (!request.Preview && logTargets.Count > 0)
        {
            _adminLogger.Add(
                LogType.AdminMessage,
                LogImpact.Low,
                $"{Player.Name} sent subtle message to {string.Join(", ", logTargets)}: {request.PopupMessage}");
        }

        if (request.CloseAfter)
            Close();
    }

    private List<ICommonSession> ResolveTargets(SubtleMessageRequest request)
    {
        if (request.Preview)
            return new List<ICommonSession> { Player };

        if (request.DisplayToAll)
            return _players.Sessions.ToList<ICommonSession>();

        var recipients = new List<ICommonSession>();
        var seen = new HashSet<NetUserId>();

        foreach (var userId in request.Recipients)
        {
            if (!seen.Add(userId))
                continue;

            if (_players.TryGetSessionById(userId, out var session))
                recipients.Add(session);
        }

        return recipients;
    }

    private void SendMessage(ICommonSession target, SubtleMessageRequest request)
    {
        if (target.AttachedEntity is not { } attached || !_entityManager.EntityExists(attached))
            return;

        var coordinates = TryResolveCoordinates(request, target, out var requestedCoordinates)
            ? requestedCoordinates
            : _entityManager.GetComponent<TransformComponent>(attached).Coordinates;

        _popup.PopupCoordinatesStyled(request.PopupMessage, coordinates, target, request.Style);

        if (request.SendChat)
            SendChatCopy(target, request);
    }

    private void SendChatCopy(ICommonSession target, SubtleMessageRequest request)
    {
        var chatMessage = string.IsNullOrWhiteSpace(request.ChatMessage)
            ? request.PopupMessage
            : request.ChatMessage;

        var color = request.Style.Rainbow ? Color.Red : ParseColorOrDefault(request.Style.ColorHex);
        var wrapped = $"[font size=15][color={color.ToHex()}]{chatMessage}[/color][/font]";
        _chat.ChatMessageToOne(ChatChannel.Local, chatMessage, wrapped, EntityUid.Invalid, false, target.Channel);
    }

    private bool TryResolveCoordinates(SubtleMessageRequest request, ICommonSession target, out EntityCoordinates coordinates)
    {
        if (target.AttachedEntity is not { } targetEntity || !_entityManager.EntityExists(targetEntity))
        {
            coordinates = EntityCoordinates.Invalid;
            return false;
        }

        if (request.AnchorToRecipients)
        {
            coordinates = _entityManager.GetComponent<TransformComponent>(targetEntity).Coordinates;
            if (request.UseRecipientOffset)
                coordinates = new EntityCoordinates(coordinates.EntityId, coordinates.Position + new Vector2(request.X, request.Y));

            return true;
        }

        if (request.UseCoordinates)
        {
            var targetMap = _transform.GetMapCoordinates(targetEntity);
            coordinates = _transform.ToCoordinates(new MapCoordinates(new Vector2(request.X, request.Y), targetMap.MapId));
            return true;
        }

        coordinates = _entityManager.GetComponent<TransformComponent>(targetEntity).Coordinates;
        return true;
    }

    private bool TryGetDefaultCoordinates(out MapCoordinates coordinates)
    {
        if (_defaultCoordinates is { } defaultCoordinates)
        {
            coordinates = _transform.ToMapCoordinates(defaultCoordinates);
            return true;
        }

        if (Player.AttachedEntity is { } attached && _entityManager.EntityExists(attached))
        {
            coordinates = _transform.GetMapCoordinates(attached);
            return true;
        }

        coordinates = MapCoordinates.Nullspace;
        return false;
    }

    private bool TryGetUserCoordinates(out MapCoordinates coordinates)
    {
        if (Player.AttachedEntity is { } attached && _entityManager.EntityExists(attached))
        {
            coordinates = _transform.GetMapCoordinates(attached);
            return true;
        }

        coordinates = MapCoordinates.Nullspace;
        return false;
    }

    private static void ClampStyle(SubtlePopupStyle style)
    {
        style.Scale = Math.Clamp(style.Scale, MinScale, MaxScale);
        style.CharactersPerSecond = Math.Clamp(style.CharactersPerSecond, MinCharactersPerSecond, MaxCharactersPerSecond);
        style.WaveSpeed = Math.Clamp(style.WaveSpeed, MinWaveSpeed, MaxWaveSpeed);
        style.WaveHeight = Math.Clamp(style.WaveHeight, MinWaveHeight, MaxWaveHeight);
        style.LingerTime = Math.Clamp(style.LingerTime, MinLingerTime, MaxLingerTime);
        style.ColorHex = ParseColorOrDefault(style.ColorHex).ToHex();
    }

    private static Color ParseColorOrDefault(string hex)
    {
        try
        {
            return Color.FromHex(hex);
        }
        catch (Exception)
        {
            return Color.Red;
        }
    }
}
