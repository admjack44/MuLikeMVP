using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MuLike.Networking;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MuLike.Optimization
{
    public enum MobileQualityLevel
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public enum MobileQaDeviceTier
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>
    /// Runtime quality controller with mobile-oriented tiers and automatic FPS fallback.
    /// </summary>
    public sealed class QualityManager : MonoBehaviour
    {
        [Serializable]
        public sealed class QualityPreset
        {
            public MobileQualityLevel level = MobileQualityLevel.Medium;
            [Range(15, 120)] public int targetFps = 45;
            [Range(0.5f, 1f)] public float renderScale = 0.75f;
            public bool enableShadows = true;
            public bool enableSoftShadows;
            [Range(0f, 80f)] public float shadowDistance = 22f;
            [Range(0f, 2f)] public float lodBias = 0.9f;
            [Range(0, 3)] public int masterTextureLimit;
        }

        [Header("Presets")]
        [SerializeField] private QualityPreset _low = new QualityPreset
        {
            level = MobileQualityLevel.Low,
            targetFps = 30,
            renderScale = 0.5f,
            enableShadows = false,
            enableSoftShadows = false,
            shadowDistance = 0f,
            lodBias = 0.7f,
            masterTextureLimit = 2
        };

        [SerializeField] private QualityPreset _medium = new QualityPreset
        {
            level = MobileQualityLevel.Medium,
            targetFps = 45,
            renderScale = 0.75f,
            enableShadows = true,
            enableSoftShadows = false,
            shadowDistance = 18f,
            lodBias = 0.9f,
            masterTextureLimit = 1
        };

        [SerializeField] private QualityPreset _high = new QualityPreset
        {
            level = MobileQualityLevel.High,
            targetFps = 60,
            renderScale = 1f,
            enableShadows = true,
            enableSoftShadows = true,
            shadowDistance = 35f,
            lodBias = 1.15f,
            masterTextureLimit = 0
        };

        [Header("Adaptive")]
        [SerializeField] private bool _autoAdjustByFps = true;
        [SerializeField, Range(15f, 35f)] private float _lowFpsThreshold = 25f;
        [SerializeField, Min(1f)] private float _lowFpsDurationSeconds = 10f;
        [SerializeField, Min(3f)] private float _qualityChangeCooldownSeconds = 10f;
        [SerializeField] private bool _upgradeWhenStable;
        [SerializeField, Min(5f)] private float _stableFpsDurationSeconds = 25f;

        [Header("Startup")]
        [SerializeField] private bool _applyOnAwake = true;
        [SerializeField] private bool _autoSelectInitialByMemory = true;
        [SerializeField] private MobileQualityLevel _initialQuality = MobileQualityLevel.Medium;

        private MobileQualityLevel _currentLevel;
        private MobileQualityLevel _maxAllowedLevel = MobileQualityLevel.High;
        private float _smoothedFps = 60f;
        private float _lowFpsTimer;
        private float _stableFpsTimer;
        private float _nextChangeAllowedAt;

        public MobileQualityLevel CurrentLevel => _currentLevel;
        public float SmoothedFps => _smoothedFps;

        public event Action<MobileQualityLevel> QualityChanged;

        private void Awake()
        {
            if (!_applyOnAwake)
                return;

            MobileQualityLevel initial = _autoSelectInitialByMemory
                ? ResolveInitialQualityByMemory()
                : _initialQuality;

            ApplyQuality(initial, force: true);
        }

        private void Update()
        {
            float frameFps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _smoothedFps = Mathf.Lerp(_smoothedFps, frameFps, 0.06f);

            if (_autoAdjustByFps)
                TickAdaptiveQuality();
        }

        public void SetBatterySafeMode(bool enabled)
        {
            if (enabled)
            {
                _maxAllowedLevel = MobileQualityLevel.Medium;
                if (_currentLevel > _maxAllowedLevel)
                    ApplyQuality(_maxAllowedLevel);
            }
            else
            {
                _maxAllowedLevel = MobileQualityLevel.High;
            }
        }

        public void SetThermalThrottle(bool severe)
        {
            if (severe)
            {
                _maxAllowedLevel = MobileQualityLevel.Low;
                ApplyQuality(MobileQualityLevel.Low);
                return;
            }

            if (_maxAllowedLevel < MobileQualityLevel.Medium)
                _maxAllowedLevel = MobileQualityLevel.Medium;
        }

        public void ConfigureAdaptiveThresholds(float lowFpsThreshold, float lowFpsDurationSeconds, float changeCooldownSeconds)
        {
            _lowFpsThreshold = Mathf.Clamp(lowFpsThreshold, 15f, 35f);
            _lowFpsDurationSeconds = Mathf.Max(1f, lowFpsDurationSeconds);
            _qualityChangeCooldownSeconds = Mathf.Max(3f, changeCooldownSeconds);
        }

        public void ApplyQaProfile(MobileQaDeviceTier tier, bool ios)
        {
            _low.targetFps = 30;
            _low.renderScale = ios ? 0.58f : 0.50f;
            _low.enableShadows = false;
            _low.enableSoftShadows = false;
            _low.shadowDistance = 0f;
            _low.lodBias = 0.70f;
            _low.masterTextureLimit = 2;

            _medium.targetFps = 45;
            _medium.renderScale = ios ? 0.82f : 0.75f;
            _medium.enableShadows = true;
            _medium.enableSoftShadows = false;
            _medium.shadowDistance = ios ? 22f : 18f;
            _medium.lodBias = 0.95f;
            _medium.masterTextureLimit = 1;

            _high.targetFps = ios ? 60 : 55;
            _high.renderScale = 1.0f;
            _high.enableShadows = true;
            _high.enableSoftShadows = true;
            _high.shadowDistance = ios ? 38f : 32f;
            _high.lodBias = 1.15f;
            _high.masterTextureLimit = 0;

            ConfigureAdaptiveThresholds(25f, 10f, 10f);

            MobileQualityLevel level = tier switch
            {
                MobileQaDeviceTier.Low => MobileQualityLevel.Low,
                MobileQaDeviceTier.High => MobileQualityLevel.High,
                _ => MobileQualityLevel.Medium
            };

            ApplyQuality(level, force: true);
        }

        public void ApplyQuality(MobileQualityLevel level, bool force = false)
        {
            level = ClampByMaxAllowed(level);
            if (!force && level == _currentLevel)
                return;

            QualityPreset preset = ResolvePreset(level);
            _currentLevel = level;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.Max(30, preset.targetFps);
            QualitySettings.lodBias = Mathf.Max(0.1f, preset.lodBias);
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(preset.masterTextureLimit, 0, 3);
            ApplyShadowSettings(preset);
            ApplyRenderScale(preset.renderScale);

            _lowFpsTimer = 0f;
            _stableFpsTimer = 0f;
            _nextChangeAllowedAt = Time.unscaledTime + Mathf.Max(1f, _qualityChangeCooldownSeconds);

            QualityChanged?.Invoke(_currentLevel);
            Debug.Log($"[QualityManager] Applied {_currentLevel} preset. TargetFPS={Application.targetFrameRate}, Scale={preset.renderScale:F2}");
        }

        private void TickAdaptiveQuality()
        {
            if (Time.unscaledTime < _nextChangeAllowedAt)
                return;

            if (_smoothedFps < _lowFpsThreshold)
            {
                _lowFpsTimer += Time.unscaledDeltaTime;
                _stableFpsTimer = 0f;

                if (_lowFpsTimer >= _lowFpsDurationSeconds)
                {
                    StepQualityDown();
                    _lowFpsTimer = 0f;
                }

                return;
            }

            _lowFpsTimer = 0f;
            if (!_upgradeWhenStable || _currentLevel >= _maxAllowedLevel)
                return;

            _stableFpsTimer += Time.unscaledDeltaTime;
            if (_stableFpsTimer >= _stableFpsDurationSeconds)
            {
                _stableFpsTimer = 0f;
                StepQualityUp();
            }
        }

        private void StepQualityDown()
        {
            if (_currentLevel <= MobileQualityLevel.Low)
                return;

            ApplyQuality(_currentLevel - 1);
        }

        private void StepQualityUp()
        {
            if (_currentLevel >= _maxAllowedLevel)
                return;

            ApplyQuality(_currentLevel + 1);
        }

        private static void ApplyRenderScale(float scale)
        {
            float clamped = Mathf.Clamp(scale, 0.5f, 1f);
            ScalableBufferManager.ResizeBuffers(clamped, clamped);

            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
                urp.renderScale = clamped;
        }

        private static void ApplyShadowSettings(QualityPreset preset)
        {
            if (!preset.enableShadows)
            {
                QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                QualitySettings.shadowDistance = 0f;
                ApplyUrpSoftShadows(false);
                return;
            }

            QualitySettings.shadows = preset.enableSoftShadows ? UnityEngine.ShadowQuality.All : UnityEngine.ShadowQuality.HardOnly;
            QualitySettings.shadowDistance = Mathf.Max(0f, preset.shadowDistance);
            ApplyUrpSoftShadows(preset.enableSoftShadows);
        }

        private static void ApplyUrpSoftShadows(bool enabled)
        {
            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
                return;

            var property = typeof(UniversalRenderPipelineAsset).GetProperty("supportsSoftShadows");
            if (property != null && property.CanWrite)
                property.SetValue(urp, enabled, null);
        }

        private QualityPreset ResolvePreset(MobileQualityLevel level)
        {
            return level switch
            {
                MobileQualityLevel.Low => _low,
                MobileQualityLevel.High => _high,
                _ => _medium
            };
        }

        private MobileQualityLevel ClampByMaxAllowed(MobileQualityLevel level)
        {
            return level > _maxAllowedLevel ? _maxAllowedLevel : level;
        }

        private static MobileQualityLevel ResolveInitialQualityByMemory()
        {
            int ram = Mathf.Max(512, SystemInfo.systemMemorySize);
            if (ram <= 3072)
                return MobileQualityLevel.Low;
            if (ram <= 6144)
                return MobileQualityLevel.Medium;
            return MobileQualityLevel.High;
        }
    }

    /// <summary>
    /// Explicit distance-based LOD selector: 100% (<10m), 50% (<20m), 25% (<50m), hidden otherwise.
    /// </summary>
    public sealed class MobileDistanceLod : MonoBehaviour
    {
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private GameObject _lod100;
        [SerializeField] private GameObject _lod50;
        [SerializeField] private GameObject _lod25;
        [SerializeField, Min(1f)] private float _distanceLod100 = 10f;
        [SerializeField, Min(1f)] private float _distanceLod50 = 20f;
        [SerializeField, Min(1f)] private float _distanceLod25 = 50f;

        private void Awake()
        {
            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null)
                return;

            float sqr = (_cameraTransform.position - transform.position).sqrMagnitude;
            float d100 = _distanceLod100 * _distanceLod100;
            float d50 = _distanceLod50 * _distanceLod50;
            float d25 = _distanceLod25 * _distanceLod25;

            if (sqr <= d100)
            {
                SetActiveLod(show100: true, show50: false, show25: false);
                return;
            }

            if (sqr <= d50)
            {
                SetActiveLod(show100: false, show50: true, show25: false);
                return;
            }

            if (sqr <= d25)
            {
                SetActiveLod(show100: false, show50: false, show25: true);
                return;
            }

            SetActiveLod(show100: false, show50: false, show25: false);
        }

        private void SetActiveLod(bool show100, bool show50, bool show25)
        {
            if (_lod100 != null && _lod100.activeSelf != show100)
                _lod100.SetActive(show100);
            if (_lod50 != null && _lod50.activeSelf != show50)
                _lod50.SetActive(show50);
            if (_lod25 != null && _lod25.activeSelf != show25)
                _lod25.SetActive(show25);
        }
    }

    /// <summary>
    /// Mobile networking optimizer: delta-compressed position stream, 10 packets/sec cap, and batched monster updates.
    /// </summary>
    public sealed class MobileNetworkOptimizer : MonoBehaviour
    {
        private struct PositionSample
        {
            public Vector3 LastPosition;
            public bool HasValue;
        }

        private struct MonsterUpdate
        {
            public int Id;
            public Vector3 Position;
            public ushort Hp;
        }

        [Header("Dependencies")]
        [SerializeField] private MuNetworkManager _network;

        [Header("Rate limit")]
        [SerializeField, Range(1, 30)] private int _maxPacketsPerSecond = 10;

        [Header("Delta compression")]
        [SerializeField] private float _positionQuantization = 100f;
        [SerializeField] private float _minPositionDelta = 0.02f;

        [Header("Batching")]
        [SerializeField, Min(0.03f)] private float _monsterBatchIntervalSeconds = 0.1f;
        [SerializeField, Min(1)] private int _maxMonstersPerBatch = 32;

        private readonly Dictionary<int, PositionSample> _positionByEntity = new Dictionary<int, PositionSample>();
        private readonly List<MonsterUpdate> _pendingMonsters = new List<MonsterUpdate>(64);

        private float _windowStart;
        private int _sentInWindow;
        private float _nextBatchAt;

        private void Awake()
        {
            if (_network == null)
                _network = FindAnyObjectByType<MuNetworkManager>();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextBatchAt)
            {
                _nextBatchAt = Time.unscaledTime + Mathf.Max(0.03f, _monsterBatchIntervalSeconds);
                FlushMonsterBatch();
            }
        }

        public bool TrySendPlayerPosition(int playerEntityId, Vector3 currentPosition)
        {
            if (!CanSendPacket())
                return false;

            PositionSample sample;
            if (!_positionByEntity.TryGetValue(playerEntityId, out sample))
                sample = default;

            Vector3 delta = sample.HasValue ? (currentPosition - sample.LastPosition) : Vector3.zero;
            if (sample.HasValue && delta.sqrMagnitude < (_minPositionDelta * _minPositionDelta))
                return false;

            byte[] packet = BuildDeltaPositionPacket(playerEntityId, currentPosition, sample);
            if (packet == null || packet.Length == 0)
                return false;

            _positionByEntity[playerEntityId] = new PositionSample
            {
                LastPosition = currentPosition,
                HasValue = true
            };

            MarkPacketSent();
            _ = SendPayload(packet);
            return true;
        }

        public void QueueMonsterUpdate(int monsterId, Vector3 worldPosition, ushort hp)
        {
            _pendingMonsters.Add(new MonsterUpdate
            {
                Id = monsterId,
                Position = worldPosition,
                Hp = hp
            });

            if (_pendingMonsters.Count >= Mathf.Max(1, _maxMonstersPerBatch))
                FlushMonsterBatch();
        }

        private void FlushMonsterBatch()
        {
            if (_pendingMonsters.Count == 0)
                return;

            if (!CanSendPacket())
                return;

            int count = Mathf.Min(_pendingMonsters.Count, Mathf.Max(1, _maxMonstersPerBatch));
            byte[] packet = BuildMonsterBatchPacket(count);
            if (packet == null || packet.Length == 0)
                return;

            _pendingMonsters.RemoveRange(0, count);
            MarkPacketSent();
            _ = SendPayload(packet);
        }

        private byte[] BuildDeltaPositionPacket(int entityId, Vector3 currentPosition, PositionSample sample)
        {
            using MemoryStream ms = new MemoryStream(64);
            using BinaryWriter bw = new BinaryWriter(ms);

            bw.Write((byte)0xA1); // custom opcode: delta position
            bw.Write(entityId);

            if (!sample.HasValue)
            {
                bw.Write((byte)0); // full position
                bw.Write(currentPosition.x);
                bw.Write(currentPosition.y);
                bw.Write(currentPosition.z);
                return ms.ToArray();
            }

            Vector3 delta = currentPosition - sample.LastPosition;
            short dx = Quantize(delta.x);
            short dy = Quantize(delta.y);
            short dz = Quantize(delta.z);

            bw.Write((byte)1); // delta mode
            bw.Write(dx);
            bw.Write(dy);
            bw.Write(dz);
            return ms.ToArray();
        }

        private byte[] BuildMonsterBatchPacket(int count)
        {
            using MemoryStream ms = new MemoryStream(1 + 2 + (count * 18));
            using BinaryWriter bw = new BinaryWriter(ms);

            bw.Write((byte)0xA2); // custom opcode: monster batch
            bw.Write((ushort)count);

            for (int i = 0; i < count; i++)
            {
                MonsterUpdate update = _pendingMonsters[i];
                bw.Write(update.Id);
                bw.Write(update.Position.x);
                bw.Write(update.Position.y);
                bw.Write(update.Position.z);
                bw.Write(update.Hp);
            }

            return ms.ToArray();
        }

        private short Quantize(float value)
        {
            float scaled = value * Mathf.Max(1f, _positionQuantization);
            return (short)Mathf.Clamp(Mathf.RoundToInt(scaled), short.MinValue, short.MaxValue);
        }

        private bool CanSendPacket()
        {
            if (_network == null || !_network.IsConnected)
                return false;

            float now = Time.unscaledTime;
            if (_windowStart <= 0f || now - _windowStart >= 1f)
            {
                _windowStart = now;
                _sentInWindow = 0;
            }

            return _sentInWindow < Mathf.Max(1, _maxPacketsPerSecond);
        }

        private void MarkPacketSent()
        {
            _sentInWindow++;
        }

        private Task SendPayload(byte[] packet)
        {
            if (_network == null)
                return Task.CompletedTask;

            return _network.SendImmediateAsync(packet);
        }

        public void ApplyQaProfile(MobileQaDeviceTier tier)
        {
            switch (tier)
            {
                case MobileQaDeviceTier.Low:
                    _maxPacketsPerSecond = 8;
                    _monsterBatchIntervalSeconds = 0.16f;
                    _maxMonstersPerBatch = 24;
                    _positionQuantization = 90f;
                    _minPositionDelta = 0.03f;
                    break;
                case MobileQaDeviceTier.High:
                    _maxPacketsPerSecond = 10;
                    _monsterBatchIntervalSeconds = 0.08f;
                    _maxMonstersPerBatch = 40;
                    _positionQuantization = 120f;
                    _minPositionDelta = 0.015f;
                    break;
                default:
                    _maxPacketsPerSecond = 10;
                    _monsterBatchIntervalSeconds = 0.10f;
                    _maxMonstersPerBatch = 32;
                    _positionQuantization = 100f;
                    _minPositionDelta = 0.02f;
                    break;
            }
        }
    }
}
