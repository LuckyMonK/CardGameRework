using System;
using System.Threading.Tasks;
using System.Threading;
using CardGame.Meta.Menu.Contracts;
using CardGame.UI;

namespace CardGame.Meta.Menu
{
    public sealed class MenuPresenter : WindowPresenter<IMenuView>
    {
        private readonly MenuModel model;
        private readonly IMenuActions actions;

        public MenuPresenter(MenuModel model, IMenuActions actions)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        protected override void OnInitialize()
        {
            Render();
            View.PlayRequested += OnPlay;
            View.SettingsRequested += OnSettings;
            View.QuitRequested += OnQuit;
        }

        protected override Task OnOpened(CancellationToken token)
        {
            Render();
            return Task.CompletedTask;
        }

        private async void OnPlay() => await ExecuteAsync(actions.PlayAsync);
        private async void OnSettings() => await ExecuteAsync(actions.OpenSettingsAsync);

        private void OnQuit()
        {
            if (!IsOpen || IsDisposed || model.IsBusy) return;
            try { actions.Quit(); }
            catch (Exception exception)
            {
                model.CompleteAction(exception.Message);
                if (!IsDisposed && IsOpen) Render();
            }
        }

        private async Task ExecuteAsync(Func<Task> action)
        {
            if (!IsOpen || IsDisposed || model.IsBusy) return;
            model.BeginAction();
            Render();
            var error = "";
            try { await action(); }
            catch (OperationCanceledException) { }
            catch (Exception exception) { error = exception.Message; }
            finally
            {
                model.CompleteAction(error);
                if (!IsDisposed && IsOpen) Render();
            }
        }

        private void Render() => View.Render(model.IsBusy, model.Error);

        protected override void OnDispose()
        {
            if (View == null) return;
            View.PlayRequested -= OnPlay;
            View.SettingsRequested -= OnSettings;
            View.QuitRequested -= OnQuit;
        }
    }
}
