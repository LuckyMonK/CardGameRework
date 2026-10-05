using System.Threading.Tasks;

namespace CardGame.Meta.Menu.Contracts
{
    // Application supplies navigation. The menu does not depend on other features.
    public interface IMenuActions
    {
        Task PlayAsync();
        Task OpenSettingsAsync();
        void Quit();
    }
}
