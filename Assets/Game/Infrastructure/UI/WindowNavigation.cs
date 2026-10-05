using System;
using System.Threading.Tasks;
using CardGame.UI;
using UnityEngine;
using Zenject;

namespace CardGame.Infrastructure.UI
{
    // Attach to a window prefab and connect Button.onClick to these methods.
    public sealed class WindowNavigation : MonoBehaviour
    {
        [SerializeField] private bool backInTopLayer = true;
        [SerializeField] private UiLayer layer = UiLayer.Windows;
        private IUiService ui;
        private bool busy;

        [Inject]
        public void Construct(IUiService ui) => this.ui = ui;

        public void Back() => Run(() => ui.Back(backInTopLayer ? (UiLayer?)null : layer));
        public void BackToDefault() => Run(() => ui.BackToDefault(layer));

        private async void Run(Func<Task> navigate)
        {
            if (busy || ui == null) return;
            busy = true;
            try { await navigate(); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { busy = false; }
        }
    }
}
