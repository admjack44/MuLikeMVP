using System.Collections;
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
            if (_textPrefab == null) return;

            var instance = Instantiate(_textPrefab, position, Quaternion.identity);
            instance.text = text;
            instance.color = color;
            StartCoroutine(Animate(instance, position));
        }

        private IEnumerator Animate(TextMeshPro tmp, Vector3 startPos)
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

            Destroy(tmp.gameObject);
        }
    }
}
