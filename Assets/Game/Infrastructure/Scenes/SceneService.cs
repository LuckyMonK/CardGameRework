using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGame.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardGame.Infrastructure.Scenes
{
    public sealed class SceneService : ISceneService, IDisposable
    {
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly HashSet<Lease> scenes = new HashSet<Lease>();
        private bool disposed;

        public async Task<ISceneLease> LoadAsync(string scenePath, LoadSceneMode mode = LoadSceneMode.Additive,
            bool setActive = false, CancellationToken cancellationToken = default)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SceneService));
            if (string.IsNullOrWhiteSpace(scenePath)) throw new ArgumentException("A full scene path is required.", nameof(scenePath));
            if (mode != LoadSceneMode.Single && mode != LoadSceneMode.Additive)
                throw new ArgumentOutOfRangeException(nameof(mode));
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token))
            {
                var token = linked.Token;
                await gate.WaitAsync(token);
                try
                {
                    token.ThrowIfCancellationRequested();
                    if (SceneManager.GetSceneByPath(scenePath).isLoaded)
                        throw new InvalidOperationException($"Scene '{scenePath}' is already loaded.");
                    if (!Application.CanStreamedLevelBeLoaded(scenePath))
                        throw new InvalidOperationException($"Scene '{scenePath}' is missing from the build scene list.");
                    var operation = SceneManager.LoadSceneAsync(scenePath, mode);
                    if (operation == null) throw new InvalidOperationException($"Cannot load scene '{scenePath}'.");
                    while (!operation.isDone) await Task.Yield();
                    var scene = SceneManager.GetSceneByPath(scenePath);
                    if (!scene.IsValid() || !scene.isLoaded)
                        throw new InvalidOperationException($"Scene '{scenePath}' did not finish loading.");
                    var lease = new Lease(scene, released => scenes.Remove(released));
                    // Single replaces the owner scene too. Once started it cannot be rolled back.
                    // Do not transfer a completed replacement to an already disposed service.
                    if (!disposed) scenes.Add(lease);
                    if (mode == LoadSceneMode.Additive && token.IsCancellationRequested)
                    {
                        await lease.ReleaseAsync();
                        token.ThrowIfCancellationRequested();
                    }
                    if (setActive && !SceneManager.SetActiveScene(scene))
                    {
                        if (mode == LoadSceneMode.Additive) await lease.ReleaseAsync();
                        throw new InvalidOperationException($"Cannot make scene '{scenePath}' active.");
                    }
                    return lease;
                }
                finally { gate.Release(); }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { lifetime.Cancel(); }
            finally
            {
                // Unity unloads asynchronously; start cleanup and observe errors internally.
                foreach (var scene in scenes.ToArray()) _ = ReleaseOwnedScene(scene);
                lifetime.Dispose();
            }
        }

        private static async Task ReleaseOwnedScene(Lease scene)
        {
            try { await scene.ReleaseAsync(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private sealed class Lease : ISceneLease
        {
            private readonly Scene scene;
            private readonly Action<Lease> onReleased;
            private Task releaseTask;
            public Lease(Scene scene, Action<Lease> onReleased) { this.scene = scene; this.onReleased = onReleased; }
            public Task ReleaseAsync() => releaseTask ?? (releaseTask = ReleaseCoreAsync());
            private async Task ReleaseCoreAsync()
            {
                try
                {
                    if (!scene.IsValid() || !scene.isLoaded) return;
                    var operation = SceneManager.UnloadSceneAsync(scene);
                    if (operation == null) throw new InvalidOperationException($"Cannot unload scene '{scene.path}'.");
                    while (!operation.isDone) await Task.Yield();
                }
                finally { onReleased(this); }
            }
        }
    }
}
