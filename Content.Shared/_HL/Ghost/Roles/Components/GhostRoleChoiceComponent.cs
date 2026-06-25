using Content.Shared.NameIdentifier;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._HL.Ghost.Roles.Components;

[RegisterComponent]
public sealed partial class GhostRoleChoiceComponent : Component
{
    [DataField(required: true)]
    public Dictionary<string, GhostRoleChoiceData> Choices = new();

    [ViewVariables]
    public readonly Dictionary<ICommonSession, string> PlayerChoices = new();
}

[DataDefinition]
public sealed partial class GhostRoleChoiceData
{
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public string Description = string.Empty;

    [DataField(required: true)]
    public string Rules = string.Empty;

    [DataField]
    public List<EntProtoId>? MindRoles;

    [DataField]
    public bool? MakeSentient;

    [DataField]
    public bool? AllowSpeech;

    [DataField]
    public bool? AllowMovement;

    [DataField]
    public ProtoId<NameIdentifierGroupPrototype>? NameIdentifierGroup;

    [DataField]
    public HashSet<ProtoId<NpcFactionPrototype>>? Factions;

    [DataField]
    public bool ReplaceFactions;
}
