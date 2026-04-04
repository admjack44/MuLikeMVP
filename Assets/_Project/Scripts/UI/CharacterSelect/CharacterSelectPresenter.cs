using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MuLike.Data.DTO;
using UnityEngine;

namespace MuLike.UI.CharacterSelect
{
    /// <summary>
    /// Presenter for MU-like character selection flow.
    /// </summary>
    public sealed class CharacterSelectPresenter
    {
        private readonly CharacterSelectView _view;
        private readonly ICharacterSelectService _service;
        private readonly Action<EnterWorldResultDto> _onEnterWorldAccepted;

        private readonly List<CharacterSummaryDto> _characters = new();
        private int _selectedCharacterId;
        private int _pendingDeleteCharacterId;
        private bool _isBusy;

        public CharacterSelectPresenter(
            CharacterSelectView view,
            ICharacterSelectService service,
            Action<EnterWorldResultDto> onEnterWorldAccepted)
        {
            _view = view;
            _service = service;
            _onEnterWorldAccepted = onEnterWorldAccepted;
        }

        public void Bind()
        {
            _view.RefreshRequested += HandleRefreshRequested;
            _view.CreateRequested += HandleCreateRequested;
            _view.CharacterSelected += HandleCharacterSelected;
            _view.DeleteRequested += HandleDeleteRequested;
            _view.DeleteConfirmed += HandleDeleteConfirmed;
            _view.DeleteCancelled += HandleDeleteCancelled;
            _view.EnterWorldRequested += HandleEnterWorldRequested;

            _ = RefreshAsync();
        }

        public void Unbind()
        {
            _view.RefreshRequested -= HandleRefreshRequested;
            _view.CreateRequested -= HandleCreateRequested;
            _view.CharacterSelected -= HandleCharacterSelected;
            _view.DeleteRequested -= HandleDeleteRequested;
            _view.DeleteConfirmed -= HandleDeleteConfirmed;
            _view.DeleteCancelled -= HandleDeleteCancelled;
            _view.EnterWorldRequested -= HandleEnterWorldRequested;
        }

        private async void HandleRefreshRequested()
        {
            await RefreshAsync();
        }

        private async void HandleCreateRequested(string name, string classId)
        {
            if (_isBusy) return;

            var request = new CreateCharacterRequestDto
            {
                name = name,
                classId = classId
            };

            await RunBusyAsync(async () =>
            {
                CharacterSelectOperationResultDto result = await _service.CreateCharacterAsync(request);
                _view.SetStatus(result.success ? "Character created." : $"Create failed: {result.message}");

                if (result.success)
                {
                    _view.ClearCreateForm();
                    Debug.Log($"[CharacterSelectPresenter] Character created id={result.characterId}.");
                    await RefreshAsync();
                }
                else
                {
                    Debug.LogWarning($"[CharacterSelectPresenter] Create failed: {result.message}");
                }
            });
        }

        private void HandleCharacterSelected(int characterId)
        {
            _selectedCharacterId = characterId;
            _view.RenderCharacters(_characters, _selectedCharacterId);
            _view.SetActionAvailability(_selectedCharacterId > 0);

            CharacterSummaryDto selected = FindCharacter(_selectedCharacterId);
            string name = selected != null ? selected.name : "Unknown";
            _view.SetStatus($"Selected: {name}");
        }

        private void HandleDeleteRequested()
        {
            if (_isBusy) return;

            CharacterSummaryDto selected = FindCharacter(_selectedCharacterId);
            if (selected == null)
            {
                _view.SetStatus("Select a character first.");
                return;
            }

            _pendingDeleteCharacterId = selected.characterId;
            _view.ShowDeleteConfirmation(selected.name);
        }

        private async void HandleDeleteConfirmed()
        {
            if (_isBusy) return;

            int characterId = _pendingDeleteCharacterId;
            _pendingDeleteCharacterId = 0;
            _view.HideDeleteConfirmation();

            if (characterId <= 0)
            {
                _view.SetStatus("No character selected for deletion.");
                return;
            }

            await RunBusyAsync(async () =>
            {
                CharacterSelectOperationResultDto result = await _service.DeleteCharacterAsync(characterId);
                _view.SetStatus(result.success ? "Character deleted." : $"Delete failed: {result.message}");

                if (result.success)
                {
                    if (_selectedCharacterId == characterId)
                        _selectedCharacterId = 0;

                    Debug.Log($"[CharacterSelectPresenter] Character deleted id={characterId}.");
                    await RefreshAsync();
                }
                else
                {
                    Debug.LogWarning($"[CharacterSelectPresenter] Delete failed: {result.message}");
                }
            });
        }

        private void HandleDeleteCancelled()
        {
            _pendingDeleteCharacterId = 0;
            _view.HideDeleteConfirmation();
            _view.SetStatus("Delete cancelled.");
        }

        private async void HandleEnterWorldRequested()
        {
            if (_isBusy) return;

            if (_selectedCharacterId <= 0)
            {
                _view.SetStatus("Select a character first.");
                return;
            }

            await RunBusyAsync(async () =>
            {
                EnterWorldResultDto result = await _service.EnterWorldAsync(_selectedCharacterId);
                if (!result.success)
                {
                    _view.SetStatus($"Enter world failed: {result.message}");
                    Debug.LogWarning($"[CharacterSelectPresenter] Enter world failed: {result.message}");
                    return;
                }

                _view.SetStatus("Entering world...");
                Debug.Log($"[CharacterSelectPresenter] Enter world accepted for character={result.characterId}, scene={result.sceneName}.");
                _onEnterWorldAccepted?.Invoke(result);
            });
        }

        private async Task RefreshAsync()
        {
            await RunBusyAsync(async () =>
            {
                IReadOnlyList<CharacterSummaryDto> loaded = await _service.GetCharactersAsync();
                _characters.Clear();
                if (loaded != null)
                {
                    for (int i = 0; i < loaded.Count; i++)
                    {
                        if (loaded[i] != null)
                            _characters.Add(loaded[i]);
                    }
                }

                if (FindCharacter(_selectedCharacterId) == null)
                    _selectedCharacterId = _characters.Count > 0 ? _characters[0].characterId : 0;

                _view.RenderCharacters(_characters, _selectedCharacterId);
                _view.SetActionAvailability(_selectedCharacterId > 0);
                _view.HideDeleteConfirmation();
                _view.SetStatus($"Loaded {_characters.Count} character(s).");

                Debug.Log($"[CharacterSelectPresenter] Loaded {_characters.Count} character(s).");
            });
        }

        private async Task RunBusyAsync(Func<Task> operation)
        {
            if (_isBusy) return;

            _isBusy = true;
            _view.SetBusy(true);

            try
            {
                await operation();
            }
            finally
            {
                _isBusy = false;
                _view.SetBusy(false);
                _view.SetActionAvailability(_selectedCharacterId > 0);
            }
        }

        private CharacterSummaryDto FindCharacter(int characterId)
        {
            if (characterId <= 0) return null;

            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].characterId == characterId)
                    return _characters[i];
            }

            return null;
        }
    }
}
