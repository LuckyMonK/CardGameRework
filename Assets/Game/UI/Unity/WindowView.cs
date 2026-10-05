using System.Threading;
using System.Threading.Tasks;
using CardGame.UI;
using UnityEngine;

namespace CardGame.UI.Unity
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class WindowView : MonoBehaviour, IWindowView
    {
        [SerializeField, Min(0)] private float openDuration = 0.15f;
        [SerializeField, Min(0)] private float closeDuration = 0.1f;
        private CanvasGroup canvasGroup;
        private CanvasGroup Group => canvasGroup != null ? canvasGroup :
            (canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>());

        protected virtual void Awake()
        {
            Group.alpha = 0;
            SetInteractable(false);
        }

        public void SetInteractable(bool value)
        {
            Group.interactable = value;
            Group.blocksRaycasts = value;
        }

        public virtual Task PlayOpenAnimation(CancellationToken cancellationToken) => Fade(1, openDuration, cancellationToken);
        public virtual Task PlayCloseAnimation(CancellationToken cancellationToken) => Fade(0, closeDuration, cancellationToken);

        private async Task Fade(float target, float duration, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var initial = Group.alpha;
            var start = Time.realtimeSinceStartup;
            while (duration > 0 && Time.realtimeSinceStartup - start < duration)
            {
                token.ThrowIfCancellationRequested();
                if (this == null) throw new System.OperationCanceledException("Window was destroyed.", token);
                Group.alpha = Mathf.Lerp(initial, target, (Time.realtimeSinceStartup - start) / duration);
                await Task.Yield();
            }
            token.ThrowIfCancellationRequested();
            if (this == null) throw new System.OperationCanceledException("Window was destroyed.", token);
            Group.alpha = target;
        }
    }
}
