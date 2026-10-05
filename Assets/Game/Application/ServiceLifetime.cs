using System;
using System.Collections.Generic;

namespace CardGame.Application
{
    // Owned by GameBootstrap. Services are registered after their dependencies,
    // and disposed in reverse creation order. Zenject does not dispose them again.
    public sealed class ServiceLifetime : IDisposable
    {
        private readonly List<IDisposable> services = new List<IDisposable>();
        private bool disposed;

        public void Own(IDisposable service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (disposed)
            {
                service.Dispose();
                throw new ObjectDisposedException(nameof(ServiceLifetime));
            }
            if (!services.Contains(service)) services.Add(service);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            for (var i = services.Count - 1; i >= 0; i--)
            {
                try { services[i].Dispose(); }
                catch (Exception exception) { errors.Add(exception); }
            }
            services.Clear();
            if (errors.Count != 0) throw new AggregateException(errors);
        }
    }
}
