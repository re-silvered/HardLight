using Content.Server.NameIdentifier;
using Content.Shared.CM14.Xenos;
using Content.Shared.CM14.Xenos.Evolution;
using Content.Shared.NameIdentifier;
using Content.Shared.NameModifier.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server._HL.Xenos;

/// <summary>
/// Preserves NameIdentifier component values when friendly xenos evolve into a new body.
/// </summary>
public sealed class PersistantXenoIdentifier : EntitySystem
{
    [Dependency] private readonly NameIdentifierSystem _nameIdentifier = default!;
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;

    private static readonly ProtoId<NameIdentifierGroupPrototype> ColonialCommandChippedGroup = "ColonialCommandChipped";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoComponent, XenoEvolvedEvent>(OnXenoEvolved);
    }

    private void OnXenoEvolved(Entity<XenoComponent> ent, ref XenoEvolvedEvent ev)
    {
        if (!TryComp(ev.Old, out NameIdentifierComponent? oldIdentifier) ||
            oldIdentifier.Group != ColonialCommandChippedGroup ||
            oldIdentifier.Identifier < 0 ||
            string.IsNullOrEmpty(oldIdentifier.FullIdentifier))
        {
            return;
        }

        var newIdentifier = EnsureComp<NameIdentifierComponent>(ev.New);
        ReturnExistingIdentifier(newIdentifier);

        newIdentifier.Group = oldIdentifier.Group;
        newIdentifier.Identifier = oldIdentifier.Identifier;
        newIdentifier.FullIdentifier = oldIdentifier.FullIdentifier;
        Dirty(ev.New, newIdentifier);

        _nameModifier.RefreshNameModifiers(ev.New);

        // Keep the copied identifier from being released when the old xeno body is deleted.
        oldIdentifier.Group = null;
        Dirty(ev.Old, oldIdentifier);
    }

    private void ReturnExistingIdentifier(NameIdentifierComponent identifier)
    {
        if (identifier.Group is null ||
            identifier.Group == ColonialCommandChippedGroup ||
            identifier.Identifier < 0 ||
            !_nameIdentifier.CurrentIds.TryGetValue(identifier.Group, out var ids) ||
            ids.Contains(identifier.Identifier))
        {
            return;
        }

        ids.Add(identifier.Identifier);
    }
}
