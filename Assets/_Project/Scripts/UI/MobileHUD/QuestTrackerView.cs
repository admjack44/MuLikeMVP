using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.MobileHUD
{
    public sealed class QuestTrackerView : MonoBehaviour
    {
        [Serializable]
        public struct QuestRowWidgets
        {
            public int questId;
            public Button button;
            public TMP_Text titleText;
            public TMP_Text objectiveText;
            public TMP_Text stateText;
        }

        [SerializeField] private RectTransform _panelRoot;
        [SerializeField] private QuestRowWidgets[] _rows = Array.Empty<QuestRowWidgets>();
        [SerializeField] private CanvasGroup _canvasGroup;

        private readonly Dictionary<int, QuestTrackerEntry> _entriesById = new();

        public event Action<int> QuestTapped;

        private void Awake()
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                int capturedQuestId = _rows[i].questId;
                if (_rows[i].button != null)
                    _rows[i].button.onClick.AddListener(() => QuestTapped?.Invoke(capturedQuestId));
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i].button != null)
                    _rows[i].button.onClick.RemoveAllListeners();
            }
        }

        public void SetEntries(IReadOnlyList<QuestTrackerEntry> entries)
        {
            _entriesById.Clear();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    QuestTrackerEntry entry = entries[i];
                    if (entry == null)
                        continue;

                    _entriesById[entry.QuestId] = entry;
                }
            }

            for (int i = 0; i < _rows.Length; i++)
            {
                QuestRowWidgets row = _rows[i];
                bool active = _entriesById.TryGetValue(row.questId, out QuestTrackerEntry entry);
                if (row.button != null)
                    row.button.gameObject.SetActive(active);

                if (!active)
                    continue;

                if (row.titleText != null)
                    row.titleText.text = entry.Title;

                if (row.objectiveText != null)
                    row.objectiveText.text = entry.ObjectiveText;

                if (row.stateText != null)
                    row.stateText.text = entry.State.ToString();
            }
        }

        public bool TryGetEntry(int questId, out QuestTrackerEntry entry)
        {
            return _entriesById.TryGetValue(questId, out entry);
        }

        public void SetVisible(bool visible)
        {
            if (_panelRoot != null)
                _panelRoot.gameObject.SetActive(visible);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = visible;
                _canvasGroup.interactable = visible;
            }
        }
    }
}
