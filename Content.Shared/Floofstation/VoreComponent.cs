using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.FloofStation;

[RegisterComponent, NetworkedComponent]
public sealed partial class VoreComponent : Component
{
    [DataField]
    public string ContainerId = "vore_container";

    [DataField]
    public SoundSpecifier SoundDevour = new SoundPathSpecifier("/Audio/Floof/Vore/gulp.ogg")
    {
        Params = AudioParams.Default.WithVolume(-3f),
    };
}

[Serializable, NetSerializable]
public sealed partial class OnVoreDoAfter : SimpleDoAfterEvent
{
    [DataField]
    public int MaxPrey = 3;

    [DataField]
    public bool PhaseNom;

    public OnVoreDoAfter(int maxPrey = 3, bool phaseNom = false)
    {
        MaxPrey = maxPrey;
        PhaseNom = phaseNom;
    }
}
