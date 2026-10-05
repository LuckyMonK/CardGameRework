using System;
using System.Threading.Tasks;
using CardGame.Meta.Menu.Contracts;

namespace CardGame.Application
{
    public sealed class MenuActions : IMenuActions
    {
        // Replace these handlers with feature coordinators when those features exist.
        public Task PlayAsync() => Task.FromException(new InvalidOperationException("Запуск игры пока не подключён."));
        public Task OpenSettingsAsync() => Task.FromException(new InvalidOperationException("Настройки пока не подключены."));
        public void Quit() => UnityEngine.Application.Quit();
    }
}
