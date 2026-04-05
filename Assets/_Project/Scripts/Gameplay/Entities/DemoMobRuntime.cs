using System;
using System.Collections;
using UnityEngine;

namespace MuLike.Gameplay.Entities
{
    /// <summary>
    /// Lightweight dummy mob runtime state for the vertical slice.
    /// Keeps local HP/respawn while remaining replaceable by server authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoMobRuntime : MonoBehaviour
    {
        private static int s_nextEntityId = 20_000;

        [Header("Identity")]
        [SerializeField] private int _entityId;
        [SerializeField] private string _displayName = "Spider";
        [SerializeField, Min(1)] private int _level = 1;

        [Header("Stats")]
        [SerializeField, Min(1)] private int _maxHp = 120;
        [SerializeField, Min(0.1f)] private float _respawnSeconds = 6f;

        [Header("References")]
        [SerializeField] private EntityView _entityView;
        [SerializeField] private TargetHudRuntimeData _targetHudData;
        [SerializeField] private Collider[] _hitColliders = Array.Empty<Collider>();

        private int _currentHp;
        private bool _isDead;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        public int EntityId => _entityId;
        public int CurrentHp => _currentHp;
        public int MaxHp => _maxHp;
        public bool IsDead => _isDead;

        public event Action<DemoMobRuntime> Died;
        public event Action<DemoMobRuntime> Respawned;
        public event Action<DemoMobRuntime, int, int> HpChanged;

        private void Awake()
        {
            if (_entityView == null)
                _entityView = GetComponent<EntityView>();

            if (_targetHudData == null)
                _targetHudData = GetComponent<TargetHudRuntimeData>();

            if (_hitColliders == null || _hitColliders.Length == 0)
                _hitColliders = GetComponentsInChildren<Collider>(true);

            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

            EnsureEntityId();
            _currentHp = Mathf.Max(1, _maxHp);
            ApplyHudState();
        }

        public bool TryApplyDamage(int amount, out bool killed)
        {
            killed = false;
            if (_isDead)
                return false;

            int clampedDamage = Mathf.Max(0, amount);
            if (clampedDamage <= 0)
                return false;

            _currentHp = Mathf.Max(0, _currentHp - clampedDamage);
            HpChanged?.Invoke(this, _currentHp, _maxHp);
            ApplyHudState();

            if (_currentHp > 0)
                return true;

            HandleDeath();
            killed = true;
            return true;
        }

        private void HandleDeath()
        {
            if (_isDead)
                return;

            _isDead = true;
            _entityView?.OnDeath();
            SetCombatPresence(enabled: false);
            Died?.Invoke(this);
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, _respawnSeconds));

            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            _currentHp = Mathf.Max(1, _maxHp);
            _isDead = false;

            SetCombatPresence(enabled: true);
            ApplyHudState();
            HpChanged?.Invoke(this, _currentHp, _maxHp);
            Respawned?.Invoke(this);
        }

        private void SetCombatPresence(bool enabled)
        {
            for (int i = 0; i < _hitColliders.Length; i++)
            {
                if (_hitColliders[i] != null)
                    _hitColliders[i].enabled = enabled;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = enabled;
        }

        private void EnsureEntityId()
        {
            if (_entityId <= 0)
                _entityId = s_nextEntityId++;

            _entityView?.Initialize(_entityId);
        }

        private void ApplyHudState()
        {
            if (_targetHudData == null)
                return;

            _targetHudData.ApplyState(_displayName, _level, _currentHp, _maxHp);
        }
    }
}
