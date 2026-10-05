using System;
using CardGame.Services;
using UnityEngine;

namespace CardGame.Infrastructure.UI
{
    // Keep the bundle alive until Unity actually destroys the instantiated hierarchy.
    // This also releases it when the containing scene is unloaded externally.
    [AddComponentMenu("")]
    public sealed class UiAssetLifetime : MonoBehaviour
    {
        private IAssetLease<GameObject> asset;
        public void Attach(IAssetLease<GameObject> lease)
        {
            if (asset != null) throw new InvalidOperationException("This UI instance already owns an asset lease.");
            asset = lease ?? throw new ArgumentNullException(nameof(lease));
        }
        private void OnDestroy() => Release();

        internal void Release()
        {
            asset?.Dispose();
            asset = null;
        }
    }
}
