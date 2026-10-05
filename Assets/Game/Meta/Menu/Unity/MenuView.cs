using System;
using UnityEngine;
using CardGame.UI.Unity;
using UnityEngine.UI;

namespace CardGame.Meta.Menu.Unity
{
    public sealed class MenuView : WindowView, IMenuView
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Text status;

        public event Action PlayRequested;
        public event Action SettingsRequested;
        public event Action QuitRequested;

        protected override void Awake()
        {
            base.Awake();
            if (playButton == null || settingsButton == null || quitButton == null || status == null)
                throw new InvalidOperationException("Assign all three buttons and the status text on MenuView.");
            playButton.onClick.AddListener(OnPlay);
            settingsButton.onClick.AddListener(OnSettings);
            quitButton.onClick.AddListener(OnQuit);
        }

        private void OnDestroy()
        {
            if (playButton != null) playButton.onClick.RemoveListener(OnPlay);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettings);
            if (quitButton != null) quitButton.onClick.RemoveListener(OnQuit);
        }

        private void OnPlay() => PlayRequested?.Invoke();
        private void OnSettings() => SettingsRequested?.Invoke();
        private void OnQuit() => QuitRequested?.Invoke();

        public void Render(bool busy, string error)
        {
            playButton.interactable = !busy;
            settingsButton.interactable = !busy;
            quitButton.interactable = !busy;
            status.text = error;
        }
    }
}
