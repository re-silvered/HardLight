using Robust.Shared.Serialization;

namespace Content.Shared.HL.Administration;

[Serializable, NetSerializable]
public sealed class SubtlePopupStyle
{
    public string ColorHex = "#ff3333";
    public bool Rainbow;
    public bool Wiggle = true;
    public float WaveSpeed = 3f;
    public float WaveHeight = 3f;
    public float CharactersPerSecond = 5f;
    public float Scale = 1f;
    public float LingerTime = 4f;
}
