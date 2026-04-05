using System;
using System.Collections.Generic;
using MuLike.Data.DTO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MuLike.UI.CharacterSelect
{
    public interface ICharacterSelectView
    {
        event Action RefreshRequested;
        event Action<string, string> CreateRequested;
        event Action<int> CharacterSelected;
        event Action DeleteRequested;
        event Action DeleteConfirmed;
        event Action DeleteCancelled;
        event Action EnterWorldRequested;

        void RenderCharacters(IReadOnlyList<CharacterSummaryViewData> characters, int selectedCharacterId);
        void SetBusy(bool isBusy);
        void SetActionAvailability(bool hasSelection);
        void SetLoading(bool isLoading, bool isReconnecting);
        void SetSelectedCharacterDetails(CharacterSummaryViewData? selected);
        void SetStatus(string message);
        void ShowDeleteConfirmation(string characterName);
        void HideDeleteConfirmation();
        void ClearCreateForm();
    }

    /// <summary>
    /// Character select UI view. Exposes events and renders presenter state.
    /// </summary>
    public class CharacterSelectView : MonoBehaviour, ICharacterSelectView
    {
        [Header("List")]
        [SerializeField] private Transform _listContainer;
        [SerializeField] private CharacterSlotView _itemPrefab;
        [SerializeField] private CharacterSelectItemView _legacyItemPrefab;

        [Header("Preview")]
        [SerializeField] private CharacterPreviewAnchor _previewAnchor;

        [Header("Selected Character Info")]
        [SerializeField] private TMP_Text _selectedNameText;
        [SerializeField] private TMP_Text _selectedClassText;
        [SerializeField] private TMP_Text _selectedLevelText;
        [SerializeField] private TMP_Text _selectedPowerText;
        [SerializeField] private TMP_Text _selectedMapText;

        [Header("Mobile Layout")]
        [SerializeField] private GameObject _portraitRoot;
        [SerializeField] private GameObject _landscapeRoot;

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
        [SerializeField] private GameObject _loadingRoot;
        [SerializeField] private TMP_Text _connectionStateText;

        [Header("Delete Confirmation")]
        [SerializeField] private GameObject _deleteConfirmationRoot;
        [SerializeField] private TMP_Text _deleteConfirmationText;
        [SerializeField] private Button _deleteConfirmButton;
        [SerializeField] private Button _deleteCancelButton;

        private readonly List<CharacterSlotView> _spawnedItems = new();
        private readonly List<CharacterSelectItemView> _spawnedLegacyItems = new();
        private bool _isLandscape;

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
            UpdateOrientationLayout();
            SetLoading(false, false);
            SetSelectedCharacterDetails(null);
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

        public void RenderCharacters(IReadOnlyList<CharacterSummaryViewData> characters, int selectedCharacterId)
        {
            ClearList();

            if (characters == null || _itemPrefab == null || _listContainer == null)
            {
                RenderLegacyCharacters(null, selectedCharacterId);
                return;
            }

            for (int i = 0; i < characters.Count; i++)
            {
                CharacterSummaryViewData dto = characters[i];
                CharacterSlotView item = Instantiate(_itemPrefab, _listContainer);
                item.Bind(dto, dto.CharacterId == selectedCharacterId);
                item.Selected += HandleItemSelected;
                _spawnedItems.Add(item);
            }
        }

        public void RenderCharacters(IReadOnlyList<CharacterSummaryDto> characters, int selectedCharacterId)
        {
            if (characters == null)
            {
                RenderCharacters((IReadOnlyList<CharacterSummaryViewData>)null, selectedCharacterId);
                return;
            }

            var converted = new List<CharacterSummaryViewData>(characters.Count);
            for (int i = 0; i < characters.Count; i++)
            {
                CharacterSummaryDto dto = characters[i];
                if (dto == null)
                    continue;

                converted.Add(new CharacterSummaryViewData(
                    dto.characterId,
                    dto.name,
                    dto.classId,
                    dto.level,
                    dto.powerScore,
                    dto.mapId,
                    dto.mapName,
                    dto.isLastPlayed));
            }

            RenderCharacters(converted, selectedCharacterId);
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

        public void SetLoading(bool isLoading, bool isReconnecting)
        {
            if (_loadingRoot != null)
                _loadingRoot.SetActive(isLoading);

            if (_connectionStateText != null)
            {
                if (isReconnecting)
                    _connectionStateText.text = "Reconnecting...";
                else if (isLoading)
                    _connectionStateText.text = "Loading...";
                else
                    _connectionStateText.text = "Connected";
            }
        }

        public void SetSelectedCharacterDetails(CharacterSummaryViewData? selected)
        {
            if (_selectedNameText != null)
                _selectedNameText.text = selected.HasValue ? selected.Value.Name : "-";

            if (_selectedClassText != null)
                _selectedClassText.text = selected.HasValue ? selected.Value.ClassId : "-";

            if (_selectedLevelText != null)
                _selectedLevelText.text = selected.HasValue ? $"Lv. {Mathf.Max(1, selected.Value.Level)}" : "Lv. -";

            if (_selectedPowerText != null)
                _selectedPowerText.text = selected.HasValue ? $"Power {Mathf.Max(0, selected.Value.PowerScore)}" : "Power -";

            if (_selectedMapText != null)
                _selectedMapText.text = selected.HasValue ? selected.Value.MapName : "-";

            if (_previewAnchor != null)
            {
                if (!selected.HasValue)
                {
                    _previewAnchor.SetCharacter(null);
                }
                else
                {
                    _previewAnchor.SetCharacter(new CharacterSummaryDto
                    {
                        characterId = selected.Value.CharacterId,
                        name = selected.Value.Name,
                        classId = selected.Value.ClassId,
                        level = selected.Value.Level,
                        powerScore = selected.Value.PowerScore,
                        mapId = selected.Value.MapId,
                        mapName = selected.Value.MapName,
                        isLastPlayed = selected.Value.IsLastPlayed
                    });
                }
            }
        }

        public void SetSelectedCharacterDetails(CharacterSummaryDto selected)
        {
            if (selected == null)
            {
                SetSelectedCharacterDetails((CharacterSummaryViewData?)null);
                return;
            }

            SetSelectedCharacterDetails(new CharacterSummaryViewData(
                selected.characterId,
                selected.name,
                selected.classId,
                selected.level,
                selected.powerScore,
                selected.mapId,
                selected.mapName,
                selected.isLastPlayed));
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

        private void OnRectTransformDimensionsChange()
        {
            UpdateOrientationLayout();
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
                CharacterSlotView item = _spawnedItems[i];
                if (item == null) continue;
                item.Selected -= HandleItemSelected;
                Destroy(item.gameObject);
            }

            _spawnedItems.Clear();

            for (int i = 0; i < _spawnedLegacyItems.Count; i++)
            {
                CharacterSelectItemView item = _spawnedLegacyItems[i];
                if (item == null) continue;
                item.Selected -= HandleItemSelected;
                Destroy(item.gameObject);
            }

            _spawnedLegacyItems.Clear();
        }

        private void UpdateOrientationLayout()
        {
            bool landscape = Screen.width > Screen.height;
            if (_isLandscape == landscape)
                return;

            _isLandscape = landscape;

            if (_portraitRoot != null)
                _portraitRoot.SetActive(!landscape);

            if (_landscapeRoot != null)
                _landscapeRoot.SetActive(landscape);
        }

        private void RenderLegacyCharacters(IReadOnlyList<CharacterSummaryDto> characters, int selectedCharacterId)
        {
            if (characters == null || _legacyItemPrefab == null || _listContainer == null)
                return;

            for (int i = 0; i < characters.Count; i++)
            {
                CharacterSummaryDto dto = characters[i];
                CharacterSelectItemView item = Instantiate(_legacyItemPrefab, _listContainer);
                item.Bind(dto, dto != null && dto.characterId == selectedCharacterId);
                item.Selected += HandleItemSelected;
                _spawnedLegacyItems.Add(item);
            }
        }
    }
}
