using Robust.Shared.Configuration;

namespace Content.Shared.FloofStation;

[CVarDefs]
public sealed class VoreCVars
{
    public static readonly CVarDef<bool> VoreEnabled =
        CVarDef.Create("game.vore_enabled", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> DigestionEnabled =
        CVarDef.Create("game.digestion_enabled", true, CVar.SERVER | CVar.REPLICATED);
}

