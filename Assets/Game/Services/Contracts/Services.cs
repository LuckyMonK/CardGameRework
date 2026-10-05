using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardGame.Services
{
    public interface IConfigService
    {
        Task<T> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class, IConfig;
    }

    public interface IConfig
    {
        void Validate();
    }

    // Keys are provider-specific; feature code should receive them through configuration.
    public interface IAssetService
    {
        Task<IAssetLease<T>> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;
    }

    public interface IAssetLease<out T> : IDisposable where T : UnityEngine.Object
    {
        T Asset { get; }
    }

    public interface ISceneService
    {
        // setActive selects the scene used for lighting and newly created GameObjects.
        Task<ISceneLease> LoadAsync(string scenePath, LoadSceneMode mode = LoadSceneMode.Additive,
            bool setActive = false, CancellationToken cancellationToken = default);
    }

    public interface ISceneLease
    {
        Task ReleaseAsync();
    }

    public interface IUiLease<out T> : IDisposable where T : Component
    {
        T View { get; }
    }
}
