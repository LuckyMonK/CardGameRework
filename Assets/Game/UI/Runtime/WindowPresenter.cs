using System;
using System.Threading;
using System.Threading.Tasks;

namespace CardGame.UI
{
    public interface IWindowView
    {
        void SetInteractable(bool value);
        Task PlayOpenAnimation(CancellationToken cancellationToken);
        Task PlayCloseAnimation(CancellationToken cancellationToken);
    }

    public interface IUiService
    {
        Task<T> Open<T>(CancellationToken cancellationToken = default) where T : WindowPresenter;
        Task Close<T>(CancellationToken cancellationToken = default) where T : WindowPresenter;
        Task Back(UiLayer? layer = null, CancellationToken cancellationToken = default);
        Task<T> BackTo<T>(CancellationToken cancellationToken = default) where T : WindowPresenter;
        Task BackToDefault(UiLayer layer = UiLayer.Windows, CancellationToken cancellationToken = default);
    }

    // Lifecycle entry points are owned by IUiService. Features override only the hooks.
    public abstract class WindowPresenter : IDisposable
    {
        private IWindowView view;
        public bool IsDisposed { get; private set; }
        public bool IsOpen { get; private set; }
        public event Action Initialized;
        public event Action Opening;
        public event Action Opened;
        public event Action Closing;
        public event Action Closed;

        public void Initialize(IWindowView windowView)
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            if (view != null) throw new InvalidOperationException("Presenter is already initialized.");
            view = windowView ?? throw new ArgumentNullException(nameof(windowView));
            BindView(windowView);
            view.SetInteractable(false);
            OnInitialize();
            Initialized?.Invoke();
        }

        public async Task OpenAsync(CancellationToken token)
        {
            if (IsDisposed || view == null) throw new InvalidOperationException("Presenter is not initialized.");
            token.ThrowIfCancellationRequested();
            Opening?.Invoke();
            await OnOpening(token);
            token.ThrowIfCancellationRequested();
            await view.PlayOpenAnimation(token);
            token.ThrowIfCancellationRequested();
            IsOpen = true;
            await OnOpened(token);
            token.ThrowIfCancellationRequested();
            view.SetInteractable(true);
            Opened?.Invoke();
        }

        public async Task CloseAsync(CancellationToken token)
        {
            if (IsDisposed || !IsOpen) return;
            IsOpen = false;
            view.SetInteractable(false);
            Closing?.Invoke();
            await OnClosing(token);
            token.ThrowIfCancellationRequested();
            await view.PlayCloseAnimation(token);
            token.ThrowIfCancellationRequested();
            OnClosed();
            Closed?.Invoke();
        }

        // Infrastructure hides historical windows without disposing their model or subscriptions.
        public void Suspend()
        {
            IsOpen = false;
            if (!IsDisposed) view?.SetInteractable(false);
        }

        protected abstract void BindView(IWindowView windowView);
        protected virtual void OnInitialize() { }
        protected virtual Task OnOpening(CancellationToken token) => Task.CompletedTask;
        protected virtual Task OnOpened(CancellationToken token) => Task.CompletedTask;
        protected virtual Task OnClosing(CancellationToken token) => Task.CompletedTask;
        protected virtual void OnClosed() { }
        protected virtual void OnDispose() { }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            IsOpen = false;
            try { OnDispose(); }
            finally
            {
                Initialized = Opening = Opened = Closing = Closed = null;
                view = null;
            }
        }
    }

    public abstract class WindowPresenter<TView> : WindowPresenter where TView : class, IWindowView
    {
        protected TView View { get; private set; }
        protected sealed override void BindView(IWindowView windowView)
        {
            View = windowView as TView ?? throw new ArgumentException($"Expected {typeof(TView).Name}.");
        }
    }
}
