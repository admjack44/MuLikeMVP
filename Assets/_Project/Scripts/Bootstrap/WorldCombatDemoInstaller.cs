using MuLike.Gameplay.Combat;
using MuLike.Gameplay.Controllers;
using MuLike.Gameplay.Entities;
using UnityEngine;

namespace MuLike.Bootstrap
{
    /// <summary>
    /// Minimal world demo installer: player spawn, dummy mobs, local combat feedback bridge.
    /// </summary>
    public sealed class WorldCombatDemoInstaller : MonoBehaviour
    {
        [Header("Execution")]
        [SerializeField] private bool _runOnStart = true;
        [SerializeField] private bool _logSummary = true;

        [Header("Player")]
        [SerializeField] private CharacterMotor _playerMotor;
        [SerializeField] private Transform _playerSpawnPoint;

        [Header("Enemies")]
        [SerializeField] private DemoMobRuntime _mobPrefab;
        [SerializeField, Range(3, 5)] private int _enemyCount = 4;
        [SerializeField] private Transform[] _enemySpawnPoints;
        [SerializeField] private Vector3 _spawnCenter = new(8f, 0f, 8f);
        [SerializeField] private float _spawnRadius = 5f;

        private void Start()
        {
            if (!_runOnStart)
                return;

            InstallDemo();
        }

        [ContextMenu("Install Combat Demo")]
        public void InstallDemo()
        {
            EnsureBaseVerticalSliceWiring();
            EnsureCombatControllers();
            PositionPlayerAtSpawn();
            EnsureDummyMobs();

            if (_logSummary)
            {
                int mobCount = FindObjectsByType<DemoMobRuntime>(FindObjectsSortMode.None).Length;
                Debug.Log($"[WorldCombatDemoInstaller] Demo ready. Player={(_playerMotor != null)} Mobs={mobCount}");
            }
        }

        private void EnsureBaseVerticalSliceWiring()
        {
            WorldVerticalSliceInstaller installer = FindAnyObjectByType<WorldVerticalSliceInstaller>();
            if (installer != null)
                installer.Install();
        }

        private void EnsureCombatControllers()
        {
            if (FindAnyObjectByType<CombatFeedbackController>() == null)
                new GameObject("CombatFeedbackController").AddComponent<CombatFeedbackController>();

            if (FindAnyObjectByType<FloatingDamageController>() == null)
                new GameObject("FloatingDamageController").AddComponent<FloatingDamageController>();

            if (FindAnyObjectByType<DemoLootSpawner>() == null)
                new GameObject("DemoLootSpawner").AddComponent<DemoLootSpawner>();

            if (FindAnyObjectByType<DemoCombatSliceController>() == null)
                new GameObject("DemoCombatSliceController").AddComponent<DemoCombatSliceController>();
        }

        private void PositionPlayerAtSpawn()
        {
            if (_playerMotor == null)
                _playerMotor = FindAnyObjectByType<CharacterMotor>();

            if (_playerMotor == null || _playerSpawnPoint == null)
                return;

            CharacterController characterController = _playerMotor.GetComponent<CharacterController>();
            bool wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
                characterController.enabled = false;

            _playerMotor.transform.SetPositionAndRotation(_playerSpawnPoint.position, _playerSpawnPoint.rotation);

            if (characterController != null)
                characterController.enabled = wasEnabled;
        }

        private void EnsureDummyMobs()
        {
            DemoMobRuntime[] existing = FindObjectsByType<DemoMobRuntime>(FindObjectsSortMode.None);
            int toSpawn = Mathf.Max(0, Mathf.Clamp(_enemyCount, 3, 5) - existing.Length);
            for (int i = 0; i < toSpawn; i++)
            {
                Vector3 spawnPos = ResolveSpawnPosition(existing.Length + i);
                SpawnMob(spawnPos, i + existing.Length + 1);
            }
        }

        private Vector3 ResolveSpawnPosition(int index)
        {
            if (_enemySpawnPoints != null && index < _enemySpawnPoints.Length && _enemySpawnPoints[index] != null)
                return _enemySpawnPoints[index].position;

            float safeRadius = Mathf.Max(1f, _spawnRadius);
            float angle = (index / Mathf.Max(1f, _enemyCount)) * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * safeRadius;
            return _spawnCenter + offset;
        }

        private void SpawnMob(Vector3 position, int index)
        {
            DemoMobRuntime mob;
            if (_mobPrefab != null)
            {
                mob = Instantiate(_mobPrefab, position, Quaternion.identity);
            }
            else
            {
                mob = CreateFallbackMob(position);
            }

            mob.gameObject.name = $"DemoMob_{index}";
        }

        private static DemoMobRuntime CreateFallbackMob(Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = position;

            if (go.GetComponent<EntityView>() == null)
                go.AddComponent<MonsterView>();

            if (go.GetComponent<TargetHudRuntimeData>() == null)
                go.AddComponent<TargetHudRuntimeData>();

            DemoMobRuntime mob = go.AddComponent<DemoMobRuntime>();
            return mob;
        }
    }
}
