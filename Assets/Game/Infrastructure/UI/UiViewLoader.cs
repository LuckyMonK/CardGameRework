using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.UI;
using System.Threading;
using System.Threading.Tasks;
using CardGame.Services;
using UnityEngine;
using Zenject;

namespace CardGame.Infrastructure.UI
{
    public sealed class UiViewLoader : IDisposable
    {
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool disposed;
        private readonly Dictionary<UiLayer, RectTransform> layers = new Dictionary<UiLayer, RectTransform>();
        private UiLayer? interactiveLayer;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { lifetime.Cancel(); }
            finally
            {
                foreach (var layer in layers.Values)
                    if (layer != null) UnityEngine.Object.Destroy(layer.gameObject);
                layers.Clear();
                lifetime.Dispose();
            }
        }

        private readonly IAssetService assets;
        private readonly Transform root;
        private readonly DiContainer container;
        public UiViewLoader(IAssetService assets, Transform root, DiContainer container)
        {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.root = root != null ? root : throw new ArgumentNullException(nameof(root));
            this.container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public async Task<IUiLease<T>> OpenAsync<T>(string address, UiLayer layer, CancellationToken cancellationToken = default)
            where T : Component
        {
            if (disposed) throw new ObjectDisposedException(nameof(UiViewLoader));
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token))
            {
                cancellationToken = linked.Token;
                if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("A UI key is required.", nameof(address));
                var asset = await assets.LoadAsync<GameObject>(address, cancellationToken);
                GameObject instance = null;
                GameObject staging = null;
                UiAssetLifetime lifetime = null;
                var activated = false;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (root == null) throw new ObjectDisposedException("UI root");
                    if (!root.gameObject.activeInHierarchy)
                        throw new InvalidOperationException("UI root must be active when opening a view.");
                    if (asset.Asset.GetComponent<T>() == null)
                        throw new InvalidOperationException($"UI address '{address}' must have {typeof(T).Name} on its root.");
                    // Create under an inactive parent so injection happens before Awake/OnEnable.
                    staging = new GameObject("UI preparation");
                    staging.SetActive(false);
                    staging.transform.SetParent(root, false);
                    instance = UnityEngine.Object.Instantiate(asset.Asset, staging.transform, false);
                    instance.SetActive(false);
                    lifetime = instance.AddComponent<UiAssetLifetime>();
                    lifetime.Attach(asset);
                    container.InjectGameObject(instance);
                    cancellationToken.ThrowIfCancellationRequested();
                    instance.transform.SetParent(GetLayerRoot(layer), false);
                    instance.SetActive(true);
                    activated = true;
                    return new Lease<T>(instance, instance.GetComponent<T>());
                }
                catch
                {
                    if (instance != null) { instance.SetActive(false); UnityEngine.Object.Destroy(instance); }
                    // Never-activated objects do not receive OnDestroy; release explicitly.
                    if (!activated)
                    {
                        if (lifetime != null) lifetime.Release();
                        else asset.Dispose();
                    }
                    throw;
                }
                finally
                {
                    if (staging != null) UnityEngine.Object.Destroy(staging);
                }
            }
        }

        internal void SetInteractiveLayer(UiLayer? layer)
        {
            interactiveLayer = layer;
            foreach (var pair in layers)
            {
                if (pair.Value == null) continue;
                var group = pair.Value.GetComponent<CanvasGroup>();
                group.interactable = layer == pair.Key;
                group.blocksRaycasts = layer == pair.Key;
            }
        }

        private Transform GetLayerRoot(UiLayer layer)
        {
            if (layers.TryGetValue(layer, out var existing) && existing != null) return existing;
            var go = new GameObject(layer.ToString(), typeof(RectTransform), typeof(CanvasGroup));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            layers[layer] = rect;
            foreach (var pair in layers.OrderBy(pair => (int)pair.Key)) pair.Value.SetAsLastSibling();
            SetInteractiveLayer(interactiveLayer);
            return rect;
        }

        private sealed class Lease<T> : IUiLease<T> where T : Component
        {
            private GameObject instance;
            private readonly T view;
            public Lease(GameObject instance, T view) { this.instance = instance; this.view = view; }
            public T View => instance != null ? view : throw new ObjectDisposedException(nameof(Lease<T>));
            public void Dispose()
            {
                if (instance == null) return;
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
                instance = null;
            }
        }
    }
}
