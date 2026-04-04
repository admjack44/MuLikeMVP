using System;
using System.Collections.Generic;
using MuLike.Systems;
using UnityEngine;

namespace MuLike.Core
{
    /// <summary>
    /// Central hub that holds references to all major runtime systems.
    /// </summary>
    public static class GameContext
    {
        private enum RuntimeState
        {
            NotInitialized,
            Initializing,
            Initialized,
            ShuttingDown
        }

        private static RuntimeState _state = RuntimeState.NotInitialized;
        private static readonly Dictionary<Type, object> _runtimeSystems = new();

        public static bool IsInitialized { get; private set; }
        public static CatalogResolver CatalogResolver { get; private set; }

        public static void Initialize()
        {
            if (_state == RuntimeState.Initialized)
            {
                Debug.Log("[GameContext] Initialize skipped: already initialized.");
                return;
            }

            if (_state == RuntimeState.Initializing)
            {
                Debug.LogWarning("[GameContext] Initialize skipped: initialization already in progress.");
                return;
            }

            _state = RuntimeState.Initializing;
            Debug.Log("[GameContext] Initializing core runtime systems...");

            try
            {
                CatalogResolver = new CatalogResolver();
                RegisterSystem(CatalogResolver);
                CatalogResolver.LoadItemCatalog();

                IsInitialized = true;
                _state = RuntimeState.Initialized;
                Debug.Log($"[GameContext] Initialization completed. Registered systems: {_runtimeSystems.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameContext] Initialization failed: {ex.Message}");
                Shutdown();
                throw;
            }
        }

        public static void Shutdown()
        {
            if (_state == RuntimeState.NotInitialized)
                return;

            if (_state == RuntimeState.ShuttingDown)
            {
                Debug.LogWarning("[GameContext] Shutdown skipped: shutdown already in progress.");
                return;
            }

            _state = RuntimeState.ShuttingDown;
            Debug.Log("[GameContext] Shutting down core runtime systems...");

            CatalogResolver?.Clear();
            CatalogResolver = null;

            _runtimeSystems.Clear();
            IsInitialized = false;
            _state = RuntimeState.NotInitialized;

            Debug.Log("[GameContext] Shutdown completed.");
        }

        [Obsolete("Use Shutdown() instead. Reset() is kept for backward compatibility.")]
        public static void Reset()
        {
            Shutdown();
        }

        public static void RegisterSystem<TSystem>(TSystem system) where TSystem : class
        {
            if (system == null) throw new ArgumentNullException(nameof(system));

            _runtimeSystems[typeof(TSystem)] = system;
        }

        public static bool TryGetSystem<TSystem>(out TSystem system) where TSystem : class
        {
            if (_runtimeSystems.TryGetValue(typeof(TSystem), out object value) && value is TSystem typed)
            {
                system = typed;
                return true;
            }

            system = null;
            return false;
        }
    }
}
