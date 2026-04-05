using System;
using MuLike.Gameplay.Entities;
using UnityEngine;
using UnityEngine.AI;

namespace MuLike.AI
{
    /// <summary>
    /// Lightweight monster brain for mobile scenes.
    ///
    /// Provides rank metadata (Normal/Elite/Boss) for auto-combat target prioritization and
    /// a simple patrol-chase-attack loop that stays inside an optional leash radius.
    ///
    /// This script is intentionally deterministic/minimal so that server-authoritative monster
    /// logic can replace it later while preserving the same presentation and priority signals.
    /// </summary>
    [RequireComponent(typeof(EntityView))]
    public sealed class MuMonsterAI : MonoBehaviour
    {
        public enum MonsterRank
        {
            Normal,
            Elite,
            Boss
        }

        public enum MonsterState
        {
            Idle,
            Patrol,
            Chase,
            Attack,
            Dead
        }

        [Header("Identity")]
        [SerializeField] private MonsterRank _rank = MonsterRank.Normal;
        [SerializeField] private string _monsterFamily = "Generic";

        [Header("Combat")]
        [SerializeField] private int _maxHp = 100;
        [SerializeField] private int _attackDamage = 12;
        [SerializeField] private float _attackRange = 2.2f;
        [SerializeField] private float _attackCooldown = 1.1f;

        [Header("Awareness")]
        [SerializeField] private float _agroRange = 10f;
        [SerializeField] private float _leashRange = 18f;
        [SerializeField] private LayerMask _playerLayer = ~0;

        [Header("Movement")]
        [SerializeField] private bool _useNavMeshAgent = true;
        [SerializeField] private float _moveSpeed = 3.4f;
        [SerializeField] private float _patrolRadius = 5f;
        [SerializeField] private float _patrolInterval = 3.5f;

        [Header("Respawn")]
        [SerializeField] private bool _autoRespawn = true;
        [SerializeField] private float _respawnSeconds = 8f;

        [Header("Debug")]
        [SerializeField] private bool _verboseLogs = false;

        private EntityView _entityView;
        private NavMeshAgent _agent;
        private Transform _currentTarget;
        private Vector3 _spawnPosition;
        private int _hp;
        private float _nextAttackAt;
        private float _nextPatrolAt;
        private MonsterState _state;

        public MonsterRank Rank => _rank;
        public MonsterState State => _state;
        public bool IsAlive => _state != MonsterState.Dead;

        public event Action<MuMonsterAI> Died;
        public event Action<MuMonsterAI, Transform, int> AttackPerformed;

        private void Awake()
        {
            _entityView = GetComponent<EntityView>();
            _agent = GetComponent<NavMeshAgent>();
            _spawnPosition = transform.position;
            _hp = Mathf.Max(1, _maxHp);

            if (_agent != null)
            {
                _agent.speed = _moveSpeed;
                _agent.stoppingDistance = Mathf.Max(0.3f, _attackRange * 0.8f);
                _agent.updateRotation = true;
            }

            SetState(MonsterState.Idle);
        }

        private void Update()
        {
            if (_state == MonsterState.Dead)
                return;

            ValidateTarget();
            if (_currentTarget == null)
                _currentTarget = AcquireNearestPlayer();

            if (_currentTarget == null)
            {
                TickPatrol();
                return;
            }

            float sqrDist = (_currentTarget.position - transform.position).sqrMagnitude;
            float attackSqr = _attackRange * _attackRange;

            if (sqrDist <= attackSqr)
            {
                SetState(MonsterState.Attack);
                TryAttack();
                StopMovement();
                return;
            }

            SetState(MonsterState.Chase);
            MoveTo(_currentTarget.position);
        }

        public bool TryApplyDamage(int amount)
        {
            if (_state == MonsterState.Dead)
                return false;

            _hp -= Mathf.Max(0, amount);
            if (_hp > 0)
                return true;

            _hp = 0;
            Die();
            return true;
        }

        private void Die()
        {
            SetState(MonsterState.Dead);
            StopMovement();
            _entityView?.OnDeath();
            Died?.Invoke(this);
            Emit("Died.");

            if (_autoRespawn)
                Invoke(nameof(Respawn), Mathf.Max(1f, _respawnSeconds));
        }

        private void Respawn()
        {
            _hp = Mathf.Max(1, _maxHp);
            transform.position = _spawnPosition;
            _currentTarget = null;
            SetState(MonsterState.Idle);
            Emit("Respawned.");
        }

        private void TryAttack()
        {
            if (Time.time < _nextAttackAt)
                return;

            _nextAttackAt = Time.time + Mathf.Max(0.1f, _attackCooldown);
            AttackPerformed?.Invoke(this, _currentTarget, _attackDamage);
        }

        private void TickPatrol()
        {
            if (Time.time < _nextPatrolAt)
                return;

            _nextPatrolAt = Time.time + Mathf.Max(1f, _patrolInterval);
            Vector2 rnd = UnityEngine.Random.insideUnitCircle * Mathf.Max(0.5f, _patrolRadius);
            Vector3 candidate = _spawnPosition + new Vector3(rnd.x, 0f, rnd.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                SetState(MonsterState.Patrol);
                MoveTo(hit.position);
            }
        }

        private void MoveTo(Vector3 worldPos)
        {
            if (_useNavMeshAgent && _agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(worldPos);
                return;
            }

            Vector3 flat = worldPos;
            flat.y = transform.position.y;
            Vector3 dir = (flat - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
                return;

            transform.position += dir.normalized * (_moveSpeed * Time.deltaTime);
            transform.forward = Vector3.Slerp(transform.forward, dir.normalized, Time.deltaTime * 8f);
        }

        private void StopMovement()
        {
            if (_agent != null)
                _agent.isStopped = true;
        }

        private void ValidateTarget()
        {
            if (_currentTarget == null)
                return;

            if ((_currentTarget.position - _spawnPosition).sqrMagnitude > _leashRange * _leashRange)
            {
                _currentTarget = null;
                ReturnToSpawn();
                return;
            }

            if ((transform.position - _spawnPosition).sqrMagnitude > _leashRange * _leashRange)
            {
                _currentTarget = null;
                ReturnToSpawn();
            }
        }

        private void ReturnToSpawn()
        {
            SetState(MonsterState.Idle);
            MoveTo(_spawnPosition);
        }

        private Transform AcquireNearestPlayer()
        {
            Collider[] cols = Physics.OverlapSphere(transform.position, _agroRange, _playerLayer, QueryTriggerInteraction.Ignore);
            Transform nearest = null;
            float best = float.MaxValue;

            for (int i = 0; i < cols.Length; i++)
            {
                Transform t = cols[i] != null ? cols[i].transform : null;
                if (t == null || t == transform)
                    continue;

                float sqr = (t.position - transform.position).sqrMagnitude;
                if (sqr >= best)
                    continue;

                best = sqr;
                nearest = t;
            }

            return nearest;
        }

        private void SetState(MonsterState next)
        {
            if (_state == next)
                return;

            _state = next;
            Emit($"State -> {_state}");
        }

        private void Emit(string msg)
        {
            if (!_verboseLogs)
                return;

            Debug.Log($"[MuMonsterAI:{name}] {_monsterFamily}/{_rank}: {msg}");
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _rank == MonsterRank.Elite ? Color.yellow : Color.red;
            Gizmos.DrawWireSphere(transform.position, _agroRange);

            Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.7f);
            Gizmos.DrawWireSphere(_spawnPosition == Vector3.zero ? transform.position : _spawnPosition, _leashRange);
        }
#endif
    }
}
