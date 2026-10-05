using System;
using CardGame.Infrastructure.Assets;
using CardGame.Infrastructure.Configuration;
using CardGame.Infrastructure.Scenes;
using CardGame.Infrastructure.UI;
using CardGame.Meta.Menu;
using CardGame.Meta.Menu.Contracts;
using CardGame.Services;
using CardGame.UI;
using CardGame.Meta.Menu.Unity;
using UnityEngine;
using Zenject;

namespace CardGame.Application
{
    public sealed class GameInstaller : MonoInstaller
    {
        [SerializeField] private Transform uiRoot;

        public override void InstallBindings()
        {
            if (uiRoot == null) throw new InvalidOperationException("Assign the UI root on GameInstaller.");
            var services = new ServiceLifetime();
            Container.Bind<ServiceLifetime>().FromInstance(services).AsSingle();
            BindService<IAssetService, AddressablesAssetService>(services);
            BindService<IConfigService, JsonConfigService>(services);
            BindService<ISceneService, SceneService>(services);
            var windows = new WindowRegistry();
            windows.Register<MenuPresenter, MenuView, IMenuView>("ui/menu", UiLayer.Windows, isDefault: true);
            Container.Bind<WindowRegistry>().FromInstance(windows).AsSingle();
            Container.Bind<UiViewLoader>().AsSingle().WithArguments(uiRoot)
                .OnInstantiated<UiViewLoader>((_, service) => services.Own(service));
            BindService<IUiService, UiService>(services);
            Container.Bind<IMenuActions>().To<MenuActions>().AsSingle();
            Container.Bind<MenuModel>().AsTransient();
        }

        private void BindService<TContract, TService>(ServiceLifetime services)
            where TService : TContract, IDisposable
        {
            Container.Bind<TContract>().To<TService>().AsSingle()
                .OnInstantiated<TService>((_, service) => services.Own(service));
        }
    }
}
