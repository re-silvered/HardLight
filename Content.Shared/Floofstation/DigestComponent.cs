namespace Content.Shared.FloofStation;

[RegisterComponent]
public sealed partial class DigestComponent : Component
{
    public Dictionary<EntityUid, float> Health = new();
    public Dictionary<EntityUid, float> Timer = new();
    public HashSet<EntityUid> ActiveDigesting = new();
    public Dictionary<EntityUid, int> DigestPopupStage = new();

    [DataField]
    public float Max = 100f;
}

