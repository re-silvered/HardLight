using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.HL.Administration;

[Serializable, NetSerializable]
public sealed class SubtleMessageEuiState(
    List<SubtleMessagePlayer> players,
    NetUserId? selectedPlayer,
    bool hasDefaultCoordinates,
    float defaultX,
    float defaultY,
    bool hasUserCoordinates,
    float userX,
    float userY)
    : EuiStateBase
{
    public readonly List<SubtleMessagePlayer> Players = players;
    public readonly NetUserId? SelectedPlayer = selectedPlayer;
    public readonly bool HasDefaultCoordinates = hasDefaultCoordinates;
    public readonly float DefaultX = defaultX;
    public readonly float DefaultY = defaultY;
    public readonly bool HasUserCoordinates = hasUserCoordinates;
    public readonly float UserX = userX;
    public readonly float UserY = userY;
}

[Serializable, NetSerializable]
public sealed class SubtleMessagePlayer(NetUserId userId, string username)
{
    public readonly NetUserId UserId = userId;
    public readonly string Username = username;
}

[Serializable, NetSerializable]
public sealed class SubtleMessageRequest : EuiMessageBase
{
    public string PopupMessage = string.Empty;
    public string ChatMessage = string.Empty;
    public List<NetUserId> Recipients = new();
    public bool DisplayToAll;
    public bool SendChat;
    public bool Preview;
    public bool CloseAfter;
    public bool UseCoordinates;
    public bool AnchorToRecipients;
    public bool UseRecipientOffset;
    public float X;
    public float Y;
    public SubtlePopupStyle Style = new();
}
