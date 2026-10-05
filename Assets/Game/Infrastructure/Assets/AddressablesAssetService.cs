using System;
using System.Threading;
using System.Threading.Tasks;
using CardGame.Services;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CardGame.Infrastructure.Assets
{
    // All operations, including lease disposal, run on the Unity main thread.
    public sealed class AddressablesAssetService : IAssetService, IDisposable
    {
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { lifetime.Cancel(); }
            finally { lifetime.Dispose(); }
        }

        public async Task<IAssetLease<T>> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (disposed) throw new ObjectDisposedException(nameof(AddressablesAssetService));
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token))
            {
                cancellationToken = linked.Token;
                if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("An asset address is required.", nameof(key));
                cancellationToken.ThrowIfCancellationRequested();
                var handle = Addressables.LoadAssetAsync<T>(key);
                try
                {
                    // Addressables cannot cancel an in-flight load. Drain before releasing it.
                    // Polling also supports platforms where AsyncOperationHandle.Task is unavailable.
                    while (!handle.IsDone) await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                        throw new InvalidOperationException($"Cannot load address '{key}' as {typeof(T).Name}.", handle.OperationException);
                    return new Lease<T>(handle);
                }
                catch
                {
                    if (handle.IsValid()) Addressables.Release(handle);
                    throw;
                }
            }
        }

        private sealed class Lease<T> : IAssetLease<T> where T : UnityEngine.Object
        {
            private AsyncOperationHandle<T> handle;
            private bool disposed;
            public Lease(AsyncOperationHandle<T> handle) => this.handle = handle;
            public T Asset => !disposed && handle.IsValid() ? handle.Result : throw new ObjectDisposedException(nameof(Lease<T>));
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (handle.IsValid()) Addressables.Release(handle);
                handle = default;
            }
        }
    }
}
