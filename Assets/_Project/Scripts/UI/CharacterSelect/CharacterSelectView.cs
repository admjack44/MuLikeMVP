using System;
using System.Collections.Generic;
using MuLike.Data.DTO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.CharacterSelect
{
    /// <summary>
    /// Character select UI view. Exposes events and renders presenter state.
    /// </summary>
    public class CharacterSelectView : MonoBehaviour
    {
        [Header("List")]
        [SerializeField] private Transform _listContainer;
        [SerializeField] private CharacterSelectItemView _itemPrefab;

        [Header("Create")]
        [SerializeField] private TMP_InputField _createNameInput;
        [SerializeField] private TMP_Dropdown _createClassDropdown;
        [SerializeField] private Button _createButton;

        [Header("Actions")]
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Button _deleteButton;
        [SerializeField] private Button _enterWorldButton;

        [Header("Feedback")]
        [SerializeField] private TMP_Text _statusText;

        [Header("Delete Confirmation")]
        [SerializeField] private GameObject _deleteConfirmationRoot;
        [SerializeField] private TMP_Text _deleteConfirmationText;
        [SerializeField] private Button _deleteConfirmButton;
        [SerializeField] private Button _deleteCancelButton;

        private readonly List<CharacterSelectItemView> _spawnedItems = new();

        public event Action RefreshRequested;
        public event Action<string, string> CreateRequested;
        public event Action<int> CharacterSelected;
        public event Action DeleteRequested;
        public event Action DeleteConfirmed;
        public event Action DeleteCancelled;
        public event Action EnterWorldRequested;

        private void Awake()
        {
            if (_refreshButton != null) _refreshButton.onClick.AddListener(() => RefreshRequested?.Invoke());
            if (_createButton != null) _createButton.onClick.AddListener(HandleCreateClicked);
            if (_deleteButton != null) _deleteButton.onClick.AddListener(() => DeleteRequested?.Invoke());
            if (_enterWorldButton != null) _enterWorldButton.onClick.AddListener(() => EnterWorldRequested?.Invoke());
            if (_deleteConfirmButton != null) _deleteConfirmButton.onClick.AddListener(() => DeleteConfirmed?.Invoke());
            if (_deleteCancelButton != null) _deleteCancelButton.onClick.AddListener(() => DeleteCancelled?.Invoke());

            HideDeleteConfirmation();
            SetActionAvailability(false);
        }

        private void OnDestroy()
        {
            if (_refreshButton != null) _refreshButton.onClick.RemoveAllListeners();
            if (_createButton != null) _createButton.onClick.RemoveAllListeners();
            if (_deleteButton != null) _deleteButton.onClick.RemoveAllListeners();
            if (_enterWorldButton != null) _enterWorldButton.onClick.RemoveAllListeners();
            if (_deleteConfirmButton != null) _deleteConfirmButton.onClick.RemoveAllListeners();
            if (_deleteCancelButton != null) _deleteCancelButton.onClick.RemoveAllListeners();
        }

        public void RenderCharacters(IReadOnlyList<CharacterSummaryDto> characters, int selectedCharacterId)
        {
            ClearList();

            if (characters == null || _itemPrefab == null || _listContainer == null)
                return;

            for (int i = 0; i < characters.Count; i++)
            {
                CharacterSummaryDto dto = characters[i];
                CharacterSelectItemView item = Instantiate(_itemPrefab, _listContainer);
                item.Bind(dto, dto != null && dto.characterId == selectedCharacterId);
                item.Selected += HandleItemSelected;
                _spawnedItems.Add(item);
            }
        }

        public void SetBusy(bool isBusy)
        {
            if (_refreshButton != null) _refreshButton.interactable = !isBusy;
            if (_createButton != null) _createButton.interactable = !isBusy;
            if (_deleteButton != null) _deleteButton.interactable = !isBusy && _deleteButton.interactable;
            if (_enterWorldButton != null) _enterWorldButton.interactable = !isBusy && _enterWorldButton.interactable;

            if (_createNameInput != null) _createNameInput.interactable = !isBusy;
            if (_createClassDropdown != null) _createClassDropdown.interactable = !isBusy;
            if (_deleteConfirmButton != null) _deleteConfirmButton.interactable = !isBusy;
            if (_deleteCancelButton != null) _deleteCancelButton.interactable = !isBusy;
        }

        public void SetActionAvailability(bool hasSelection)
        {
            if (_deleteButton != null) _deleteButton.interactable = hasSelection;
            if (_enterWorldButton != null) _enterWorldButton.interactable = hasSelection;
        }

        public void SetStatus(string message)
        {
            if (_statusText == null) return;
            _statusText.text = message ?? string.Empty;
        }

        public void ShowDeleteConfirmation(string characterName)
        {
            if (_deleteConfirmationRoot != null)
                _deleteConfirmationRoot.SetActive(true);

            if (_deleteConfirmationText != null)
                _deleteConfirmationText.text = $"Delete '{characterName}'?";
        }

        public void HideDeleteConfirmation()
        {
            if (_deleteConfirmationRoot != null)
                _deleteConfirmationRoot.SetActive(false);
        }

        public void ClearCreateForm()
        {
            if (_createNameInput != null)
                _createNameInput.text = string.Empty;
        }

        private void HandleCreateClicked()
        {
            string name = _createNameInput != null ? _createNameInput.text : string.Empty;

            string classId = "DarkKnight";
            if (_createClassDropdown != null && _createClassDropdown.options != null && _createClassDropdown.options.Count > 0)
            {
                classId = _createClassDropdown.options[_createClassDropdown.value].text;
            }

            CreateRequested?.Invoke(name, classId);
        }

        private void HandleItemSelected(int characterId)
        {
            CharacterSelected?.Invoke(characterId);
        }

        private void ClearList()
        {
            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                CharacterSelectItemView item = _spawnedItems[i];
                if (item == null) continue;
                item.Selected -= HandleItemSelected;
                Destroy(item.gameObject);
            }

            _spawnedItems.Clear();
        }
    }
}
