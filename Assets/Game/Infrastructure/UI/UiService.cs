using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGame.UI;
using Zenject;
using Window = CardGame.Infrastructure.UI.WindowRegistry.WindowInstance;

namespace CardGame.Infrastructure.UI
{
    // Main thread only. Navigation owns history; presenters only implement window behaviour.
    public sealed class UiService : IUiService, IDisposable
    {
        private readonly UiViewLoader loader;
        private readonly WindowRegistry registry;
        private readonly DiContainer container;
        private readonly Dictionary<UiLayer, List<Window>> histories = new Dictionary<UiLayer, List<Window>>();
        private readonly HashSet<Window> owned = new HashSet<Window>();
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly AsyncLocal<bool> navigating = new AsyncLocal<bool>();
        private bool disposed;

        public UiService(UiViewLoader loader, WindowRegistry registry, DiContainer container)
        { this.loader = loader; this.registry = registry; this.container = container; }

        public async Task<T> Open<T>(CancellationToken cancellationToken = default) where T : WindowPresenter
        {
            T result = null;
            await Navigate(async token =>
            {
                var registration = registry.Get(typeof(T));
                var history = History(registration.Layer);
                var index = history.FindIndex(window => window.Presenter.GetType() == typeof(T));
                if (index >= 0)
                {
                    await Switch(registration.Layer, history[index], index + 1, false, token);
                    result = (T)history[index].Presenter;
                }
                else
                {
                    var next = await Create(typeof(T), token);
                    await Switch(registration.Layer, next, history.Count, true, token);
                    result = (T)next.Presenter;
                }
            }, cancellationToken);
            return result;
        }

        public Task Close<T>(CancellationToken cancellationToken = default) where T : WindowPresenter => Navigate(async token =>
        {
            var layer = registry.Get(typeof(T)).Layer;
            var history = History(layer);
            var index = history.FindIndex(window => window.Presenter.GetType() == typeof(T));
            if (index < 0) return;
            if (index == history.Count - 1) await Pop(layer, token, allowDefault: true);
            else
            {
                var removed = history[index];
                history.RemoveAt(index);
                Release(removed);
            }
        }, cancellationToken);

        public Task Back(UiLayer? layer = null, CancellationToken cancellationToken = default) => Navigate(async token =>
        {
            var target = layer ?? TopLayer();
            if (target.HasValue) await Pop(target.Value, token);
        }, cancellationToken);

        public async Task<T> BackTo<T>(CancellationToken cancellationToken = default) where T : WindowPresenter
        {
            T result = null;
            await Navigate(async token =>
            {
                var layer = registry.Get(typeof(T)).Layer;
                var history = History(layer);
                var index = history.FindIndex(window => window.Presenter.GetType() == typeof(T));
                if (index < 0) throw new InvalidOperationException($"Window {typeof(T).Name} is not in navigation history.");
                var target = history[index];
                await Switch(layer, target, index + 1, false, token);
                await ClearAbove(layer, token);
                result = (T)target.Presenter;
            }, cancellationToken);
            return result;
        }

        public Task BackToDefault(UiLayer layer = UiLayer.Windows, CancellationToken cancellationToken = default) => Navigate(async token =>
        {
            var history = History(layer);
            var type = registry.GetDefault(layer);
            if (type == null) await Switch(layer, null, 0, false, token);
            else
            {
                var index = history.FindIndex(window => window.Presenter.GetType() == type);
                var target = index >= 0 ? history[index] : await Create(type, token);
                // A default state retains only the default window, regardless of its old position.
                await Switch(layer, target, index >= 0 ? index + 1 : 0, index < 0, token);
                var older = history.Where(window => !ReferenceEquals(window, target)).ToArray();
                history.Clear();
                history.Add(target);
                ReleaseAll(older);
            }
            await ClearAbove(layer, token);
        }, cancellationToken);

        private async Task Navigate(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UiService));
            if (navigating.Value) throw new InvalidOperationException("Do not await UI navigation from a window lifecycle hook. Navigate from user actions or a coordinator.");
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token))
            {
                await gate.WaitAsync(linked.Token);
                navigating.Value = true;
                loader.SetInteractiveLayer(null);
                try
                {
                    linked.Token.ThrowIfCancellationRequested();
                    await action(linked.Token);
                }
                finally
                {
                    if (!disposed) loader.SetInteractiveLayer(TopLayer());
                    navigating.Value = false;
                    gate.Release();
                }
            }
        }

        private async Task<Window> Create(Type type, CancellationToken token)
        {
            var window = await registry.Get(type).Create(loader, container, token);
            try
            {
                token.ThrowIfCancellationRequested();
                owned.Add(window);
                return window;
            }
            catch { window.Dispose(); throw; }
        }

        // Commit history only after the destination has opened successfully.
        private async Task Switch(UiLayer layer, Window next, int keepCount, bool append, CancellationToken token)
        {
            var history = History(layer);
            var previous = history.LastOrDefault();
            if (ReferenceEquals(previous, next)) return;
            try
            {
                if (previous != null)
                {
                    await previous.Presenter.CloseAsync(token);
                    token.ThrowIfCancellationRequested();
                    previous.SetVisible(false);
                }
                if (next != null)
                {
                    next.SetVisible(true);
                    await next.Presenter.OpenAsync(token);
                }
                token.ThrowIfCancellationRequested();
            }
            catch (Exception transitionError)
            {
                var errors = new List<Exception> { transitionError };
                if (next != null)
                {
                    try { next.SetVisible(false); }
                    catch (Exception cleanupError) { errors.Add(cleanupError); }
                    if (append)
                    {
                        try { Release(next); }
                        catch (Exception cleanupError) { errors.Add(cleanupError); }
                    }
                }
                if (!disposed && previous != null)
                {
                    try
                    {
                        previous.SetVisible(true);
                        await previous.Presenter.OpenAsync(lifetime.Token);
                    }
                    catch (Exception restoreError) { errors.Add(restoreError); }
                }
                if (errors.Count > 1) throw new AggregateException("Navigation rollback encountered errors.", errors);
                throw;
            }
            var removed = history.Skip(keepCount).ToArray();
            history.RemoveRange(keepCount, history.Count - keepCount);
            if (append) history.Add(next);
            ReleaseAll(removed);
        }

        private Task Pop(UiLayer layer, CancellationToken token, bool allowDefault = false)
        {
            var history = History(layer);
            if (!allowDefault && history.Count == 1 && history[0].Presenter.GetType() == registry.GetDefault(layer))
                return Task.CompletedTask;
            return history.Count == 0 ? Task.CompletedTask :
                Switch(layer, history.Count > 1 ? history[history.Count - 2] : null, history.Count - 1, false, token);
        }

        private async Task ClearAbove(UiLayer layer, CancellationToken token)
        {
            foreach (var higher in histories.Keys.Where(key => (int)key > (int)layer).OrderByDescending(key => (int)key).ToArray())
                await Switch(higher, null, 0, false, token);
        }

        private List<Window> History(UiLayer layer)
        {
            if (!Enum.IsDefined(typeof(UiLayer), layer)) throw new ArgumentOutOfRangeException(nameof(layer));
            if (!histories.TryGetValue(layer, out var history)) histories.Add(layer, history = new List<Window>());
            return history;
        }

        private UiLayer? TopLayer() => histories.Where(pair => pair.Value.Count > 0)
            .Select(pair => (UiLayer?)pair.Key).OrderByDescending(layer => (int)layer.Value).FirstOrDefault();

        private void Release(Window window)
        {
            owned.Remove(window);
            window.Dispose();
        }

        private void ReleaseAll(IEnumerable<Window> windows)
        {
            var errors = new List<Exception>();
            foreach (var window in windows)
            {
                try { Release(window); }
                catch (Exception exception) { errors.Add(exception); }
            }
            if (errors.Count > 0) throw new AggregateException(errors);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            try { lifetime.Cancel(); }
            catch (Exception exception) { errors.Add(exception); }
            try { ReleaseAll(owned.ToArray()); }
            catch (Exception exception) { errors.Add(exception); }
            histories.Clear();
            lifetime.Dispose();
            if (errors.Count > 0) throw new AggregateException(errors);
        }
    }
}
