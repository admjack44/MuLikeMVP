using System;
using System.Collections.Generic;

namespace MuLike.Bootstrap
{
    public interface IClientServiceRegistry
    {
        void Register<TService>(TService service) where TService : class;
        bool TryResolve<TService>(out TService service) where TService : class;
        TService ResolveOrNull<TService>() where TService : class;
        bool Contains<TService>() where TService : class;
        void Clear();
    }

    /// <summary>
    /// Lightweight typed registry used as the client composition root container.
    /// </summary>
    public sealed class ClientServiceRegistry : IClientServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<TService>(TService service) where TService : class
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            _services[typeof(TService)] = service;
        }

        public bool TryResolve<TService>(out TService service) where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out object raw) && raw is TService typed)
            {
                service = typed;
                return true;
            }

            service = null;
            return false;
        }

        public TService ResolveOrNull<TService>() where TService : class
        {
            return TryResolve(out TService service) ? service : null;
        }

        public bool Contains<TService>() where TService : class
        {
            return _services.ContainsKey(typeof(TService));
        }

        public void Clear()
        {
            foreach (object service in _services.Values)
            {
                if (service is IDisposable disposable)
                    disposable.Dispose();
            }

            _services.Clear();
        }
    }
}
