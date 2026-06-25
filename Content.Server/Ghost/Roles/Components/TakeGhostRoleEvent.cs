using Robust.Shared.Player;

namespace Content.Server.Ghost.Roles.Components;

[ByRefEvent]
public record struct TakeGhostRoleEvent(ICommonSession Player, string? ChoiceId = null) // Hardlight: ChoiceId carries the selected ghost role variant.
{
    public bool TookRole { get; set; }
}
