using Content.Client.Eui;
using Content.Shared.Eui;
using Content.Shared.HL.Administration;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._HL.Administration;

public sealed class SubtleMessageEui : BaseEui
{
    private readonly SubtleMessageWindow _window;

    public SubtleMessageEui()
    {
        _window = new SubtleMessageWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.ExecuteButton.OnPressed += _ => Submit();
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is SubtleMessageEuiState subtleState)
            _window.SetState(subtleState);
    }

    private void Submit()
    {
        SendMessage(_window.BuildRequest());
    }
}
