using System;
using UnityEngine;
using CardGame.UI;
using CardGame.Meta.Menu;
using Zenject;

namespace CardGame.Application
{
    // SceneContext injects dependencies before Start; GameInstaller owns registration.
    public sealed class GameBootstrap : MonoBehaviour
    {
        private LazyInject<IUiService> ui;
        private ServiceLifetime services;

        [Inject]
        public void Construct(ServiceLifetime services, LazyInject<IUiService> ui)
        {
            this.services = services;
            this.ui = ui;
        }

        private async void Start()
        {
            try
            {
                if (ui == null) throw new InvalidOperationException("GameBootstrap requires a SceneContext with GameInstaller.");
                await ui.Value.Open<MenuPresenter>();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void OnDestroy()
        {
            try { services?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
