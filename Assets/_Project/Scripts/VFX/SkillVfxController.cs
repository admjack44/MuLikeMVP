using System.Collections.Generic;
using MuLike.Skills;
using UnityEngine;

namespace MuLike.VFX
{
    /// <summary>
    /// Mobile-friendly VFX runtime with lightweight pooling and LOD selection.
    /// </summary>
    public sealed class SkillVfxController : MonoBehaviour
    {
        [SerializeField] private bool _usePooling = true;
        [SerializeField] private bool _isMidRangeMobile = true;

        private readonly Dictionary<GameObject, Queue<GameObject>> _poolByPrefab = new();

        public void PlayCastVfx(SkillDefinition skill, Vector3 origin, Vector3 targetPoint, Camera camera = null)
        {
            if (skill == null || skill.vfxProfile == null)
                return;

            GameObject prefab = SelectPrefab(skill.vfxProfile, origin, camera);
            if (prefab == null)
                return;

            GameObject go = Spawn(prefab, origin, Quaternion.LookRotation((targetPoint - origin).normalized, Vector3.up));
            ScheduleReturn(prefab, go, Mathf.Max(0.1f, skill.vfxProfile.duration));
        }

        public void PlayImpactVfx(SkillDefinition skill, Vector3 hitPoint, Camera camera = null)
        {
            if (skill == null || skill.vfxProfile == null)
                return;

            SkillVfxProfile profile = skill.vfxProfile;
            GameObject prefab = profile.impactVfxPrefab != null
                ? profile.impactVfxPrefab
                : SelectPrefab(profile, hitPoint, camera);

            if (prefab == null)
                return;

            GameObject go = Spawn(prefab, hitPoint, Quaternion.identity);
            ScheduleReturn(prefab, go, Mathf.Max(0.08f, profile.duration * 0.5f));
        }

        private GameObject SelectPrefab(SkillVfxProfile profile, Vector3 worldPos, Camera camera)
        {
            if (profile == null)
                return null;

            if (profile.lowSpecVfxPrefab != null)
            {
                if (_isMidRangeMobile && profile.heavyParticle)
                    return profile.lowSpecVfxPrefab;

                if (camera != null && profile.lodProfile != null)
                {
                    float distance = Vector3.Distance(camera.transform.position, worldPos);
                    if (distance >= profile.lodProfile.midDistance)
                        return profile.lowSpecVfxPrefab;
                }
            }

            return profile.mainVfxPrefab;
        }

        private GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (!_usePooling)
                return Instantiate(prefab, pos, rot);

            if (!_poolByPrefab.TryGetValue(prefab, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                _poolByPrefab[prefab] = pool;
            }

            while (pool.Count > 0)
            {
                GameObject candidate = pool.Dequeue();
                if (candidate == null)
                    continue;

                candidate.transform.SetPositionAndRotation(pos, rot);
                candidate.SetActive(true);
                return candidate;
            }

            return Instantiate(prefab, pos, rot);
        }

        private void ScheduleReturn(GameObject prefab, GameObject instance, float duration)
        {
            if (instance == null)
                return;

            instance.AddComponent<ReturnToVfxPoolTimer>().Arm(this, prefab, duration);
        }

        private void Return(GameObject prefab, GameObject instance)
        {
            if (instance == null)
                return;

            if (!_usePooling)
            {
                Destroy(instance);
                return;
            }

            instance.SetActive(false);
            if (!_poolByPrefab.TryGetValue(prefab, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                _poolByPrefab[prefab] = pool;
            }

            pool.Enqueue(instance);
        }

        private sealed class ReturnToVfxPoolTimer : MonoBehaviour
        {
            private SkillVfxController _owner;
            private GameObject _prefab;
            private float _returnAt;

            public void Arm(SkillVfxController owner, GameObject prefab, float duration)
            {
                _owner = owner;
                _prefab = prefab;
                _returnAt = Time.time + duration;
            }

            private void Update()
            {
                if (_owner == null || Time.time < _returnAt)
                    return;

                _owner.Return(_prefab, gameObject);
                Destroy(this);
            }
        }
    }
}
