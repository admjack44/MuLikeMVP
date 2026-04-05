using System.Collections;
using MuLike.Performance.Pooling;
using TMPro;
using UnityEngine;

namespace MuLike.Gameplay.Combat
{
    /// <summary>
    /// Spawns and animates floating damage/heal number texts above entities.
    /// </summary>
    public class FloatingDamageController : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _textPrefab;
        [SerializeField] private string _poolKey = "ui.damage-popup";
        [SerializeField] private Color _damageColor = Color.red;
        [SerializeField] private Color _healColor = Color.green;
        [SerializeField] private Color _critColor = Color.yellow;
        [SerializeField] private float _floatHeight = 2f;
        [SerializeField] private float _duration = 1f;

        public void ShowDamage(Vector3 worldPosition, int amount, bool isCrit = false)
        {
            Color color = isCrit ? _critColor : _damageColor;
            string text = isCrit ? $"<b>{amount}!</b>" : amount.ToString();
            Spawn(worldPosition, text, color);
        }

        public void ShowHeal(Vector3 worldPosition, int amount)
        {
            Spawn(worldPosition, $"+{amount}", _healColor);
        }

        private void Spawn(Vector3 position, string text, Color color)
        {
            TextMeshPro instance = SpawnText(position);
            if (instance == null)
                return;

            instance.text = text;
            instance.color = color;
            StartCoroutine(Animate(instance, position, instance.gameObject));
        }

        private IEnumerator Animate(TextMeshPro tmp, Vector3 startPos, GameObject owner)
        {
            float elapsed = 0f;
            Vector3 endPos = startPos + Vector3.up * _floatHeight;

            while (elapsed < _duration)
            {
                float t = elapsed / _duration;
                tmp.transform.position = Vector3.Lerp(startPos, endPos, t);
                tmp.alpha = 1f - t;
                elapsed += Time.deltaTime;
                yield return null;
            }

            Recycle(owner);
        }

        private TextMeshPro SpawnText(Vector3 position)
        {
            if (_textPrefab == null)
                return CreateFallbackText(position);

            MobilePoolManager manager = MobilePoolManager.Instance;
            if (manager != null && manager.TrySpawn(_poolKey, position, Quaternion.identity, out GameObject pooledObject))
            {
                TextMeshPro pooledText = pooledObject.GetComponent<TextMeshPro>();
                if (pooledText != null)
                    return pooledText;

                manager.Release(pooledObject);
            }

            return Instantiate(_textPrefab, position, Quaternion.identity);
        }

        private static TextMeshPro CreateFallbackText(Vector3 position)
        {
            var go = new GameObject("FloatingDamageText");
            go.transform.position = position;
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 5f;
            tmp.outlineWidth = 0.2f;
            tmp.sortingOrder = 500;
            return tmp;
        }

        private static void Recycle(GameObject owner)
        {
            if (owner == null)
                return;

            if (MobilePoolManager.Instance != null)
            {
                MobilePoolManager.Instance.Release(owner);
                return;
            }

            Destroy(owner);
        }
    }
}
