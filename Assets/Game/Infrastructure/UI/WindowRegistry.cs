using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGame.UI;
using UnityEngine;
using Zenject;

namespace CardGame.Infrastructure.UI
{
    public sealed class WindowRegistry
    {
        private readonly Dictionary<Type, Registration> registrations = new Dictionary<Type, Registration>();

        private readonly Dictionary<UiLayer, Type> defaults = new Dictionary<UiLayer, Type>();

        public void Register<TPresenter, TView, TContract>(string address, UiLayer layer = UiLayer.Windows, bool isDefault = false)
            where TPresenter : WindowPresenter<TContract>
            where TView : Component, TContract
            where TContract : class, IWindowView
        {
            if (!Enum.IsDefined(typeof(UiLayer), layer)) throw new ArgumentOutOfRangeException(nameof(layer));
            if (isDefault && defaults.ContainsKey(layer)) throw new InvalidOperationException($"Default for {layer} is already registered.");
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Window address is required.", nameof(address));
            if (registrations.ContainsKey(typeof(TPresenter))) throw new InvalidOperationException($"Window {typeof(TPresenter).Name} is already registered.");
            registrations.Add(typeof(TPresenter), new Registration(layer, async (loader, container, token) =>
            {
                var lease = await loader.OpenAsync<TView>(address, layer, token);
                TPresenter presenter = null;
                try
                {
                    presenter = container.Instantiate<TPresenter>();
                    presenter.Initialize(lease.View);
                    return new WindowInstance(presenter, lease, visible => lease.View.gameObject.SetActive(visible));
                }
                catch
                {
                    try { presenter?.Dispose(); }
                    finally { lease.Dispose(); }
                    throw;
                }
            }));
            if (isDefault) defaults.Add(layer, typeof(TPresenter));
        }

        internal Type GetDefault(UiLayer layer) => defaults.TryGetValue(layer, out var type) ? type : null;

        internal Registration Get(Type presenter)
        {
            if (registrations.TryGetValue(presenter, out var registration)) return registration;
            throw new InvalidOperationException($"Window {presenter.Name} is not registered in the installer.");
        }

        internal sealed class Registration
        {
            internal readonly UiLayer Layer;
            internal readonly Func<UiViewLoader, DiContainer, CancellationToken, Task<WindowInstance>> Create;
            internal Registration(UiLayer layer, Func<UiViewLoader, DiContainer, CancellationToken, Task<WindowInstance>> create)
            { Layer = layer; Create = create; }
        }

        internal sealed class WindowInstance : IDisposable
        {
            internal readonly WindowPresenter Presenter;
            private readonly IDisposable view;
            private readonly Action<bool> setVisible;
            private bool disposed;
            internal WindowInstance(WindowPresenter presenter, IDisposable view, Action<bool> setVisible)
            { Presenter = presenter; this.view = view; this.setVisible = setVisible; }
            internal void SetVisible(bool visible)
            {
                if (disposed) return;
                if (!visible) Presenter.Suspend();
                setVisible(visible);
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                try { Presenter.Dispose(); }
                finally { view.Dispose(); }
            }
        }
    }
}
