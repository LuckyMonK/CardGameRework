using System;
using System.Threading;
using System.Threading.Tasks;
using CardGame.Services;
using UnityEngine;

namespace CardGame.Infrastructure.Configuration
{
    public sealed class JsonConfigService : IConfigService, IDisposable
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

        private readonly IAssetService assets;

        public JsonConfigService(IAssetService assets)
        {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        public async Task<T> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class, IConfig
        {
            if (disposed) throw new ObjectDisposedException(nameof(JsonConfigService));
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token))
            {
                cancellationToken = linked.Token;
                using (var source = await assets.LoadAsync<TextAsset>(key, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var json = source.Asset.text.Trim();
                        if (!json.StartsWith("{", StringComparison.Ordinal) || !json.EndsWith("}", StringComparison.Ordinal))
                            throw new FormatException("The config must contain a JSON object.");
                        var config = JsonUtility.FromJson<T>(json);
                        if (config == null) throw new FormatException("The config is empty.");
                        config.Validate();
                        return config;
                    }
                    catch (Exception exception) when (exception is ArgumentException || exception is FormatException || exception is InvalidOperationException)
                    {
                        throw new InvalidOperationException($"Cannot read config '{key}' as {typeof(T).Name}: {exception.Message}", exception);
                    }
                }
            }
        }
    }
}
