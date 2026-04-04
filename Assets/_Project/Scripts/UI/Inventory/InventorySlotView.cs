using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MuLike.UI.Inventory
{
    /// <summary>
    /// Inventory slot visual and drag/drop event surface.
    /// </summary>
    public class InventorySlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _itemNameText;
        [SerializeField] private TMP_Text _quantityText;
        [SerializeField] private GameObject _emptyRoot;
        [SerializeField] private CanvasGroup _canvasGroup;

        private bool _isEmpty;

        public int SlotIndex { get; private set; }

        public event Action<int, int> DropRequested;

        public void Bind(InventorySlotViewData data)
        {
            SlotIndex = data.SlotIndex;
            _isEmpty = data.IsEmpty;

            if (_emptyRoot != null)
                _emptyRoot.SetActive(_isEmpty);

            if (_iconImage != null)
            {
                _iconImage.enabled = !_isEmpty && data.Icon != null;
                _iconImage.sprite = data.Icon;
            }

            if (_itemNameText != null)
                _itemNameText.text = _isEmpty ? string.Empty : data.ItemName;

            if (_quantityText != null)
                _quantityText.text = _isEmpty || data.Quantity <= 1 ? string.Empty : data.Quantity.ToString();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isEmpty)
                return;

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.alpha = 0.6f;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Intentional no-op: MVP uses slot-to-slot drag semantics without moving the widget transform.
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.alpha = 1f;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null)
                return;

            InventorySlotView source = eventData.pointerDrag.GetComponent<InventorySlotView>();
            if (source == null)
                return;

            if (source.SlotIndex == SlotIndex)
                return;

            DropRequested?.Invoke(source.SlotIndex, SlotIndex);
        }
    }
}
