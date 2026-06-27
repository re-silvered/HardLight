using Robust.Shared.GameStates;

namespace Content.Shared.FloofStation;

[RegisterComponent, NetworkedComponent]
public sealed partial class DevouredComponent : Component
{
    public bool AddedPressure;
    public bool AddedBreathing;
    public bool AddedTemperature;
    public bool AddedRadiation;
}

