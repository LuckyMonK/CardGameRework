using System;
using CardGame.UI;

namespace CardGame.Meta.Menu
{
    public interface IMenuView : IWindowView
    {
        event Action PlayRequested;
        event Action SettingsRequested;
        event Action QuitRequested;
        void Render(bool busy, string error);
    }
}
