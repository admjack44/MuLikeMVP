using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MuLike.Bootstrap;
using MuLike.Data.DTO;
using UnityEngine;

namespace MuLike.UI.CharacterSelect
{
    /// <summary>
    /// Presenter for MU-like character selection flow.
    /// Coordinates with SessionStateClient and ClientFlowFeedbackService for state transitions.
    /// </summary>
    public sealed class CharacterSelectPresenter
    {
        private readonly ICharacterSelectView _view;
        private readonly ICharacterSelectRuntimeService _service;
        private readonly Action<EnterWorldResultDto, CharacterSummaryDto> _onEnterWorldAccepted;
        private readonly SessionStateClient _sessionState;
        private readonly ClientFlowFeedbackService _feedback;

        private readonly List<CharacterSummaryDto> _characters = new();
        private int _selectedCharacterId;
        private int _pendingDeleteCharacterId;
        private bool _isBusy;

        public CharacterSelectPresenter(
            ICharacterSelectView view,
            ICharacterSelectRuntimeService service,
            Action<EnterWorldResultDto, CharacterSummaryDto> onEnterWorldAccepted,
            SessionStateClient sessionState,
            ClientFlowFeedbackService feedback)
        {
            _view = view;
            _service = service;
            _onEnterWorldAccepted = onEnterWorldAccepted;
            _sessionState = sessionState;
            _feedback = feedback;
        }

        public CharacterSelectPresenter(
            ICharacterSelectView view,
            ICharacterSelectRuntimeService service,
            Action<EnterWorldResultDto, CharacterSummaryDto> onEnterWorldAccepted)
            : this(view, service, onEnterWorldAccepted, new SessionStateClient(), new ClientFlowFeedbackService())
        {
        }

        public CharacterSelectPresenter(
            ICharacterSelectView view,
            ICharacterSelectService service,
            Action<EnterWorldResultDto, CharacterSummaryDto> onEnterWorldAccepted,
            SessionStateClient sessionState,
            ClientFlowFeedbackService feedback)
            : this(view, WrapRuntimeService(service), onEnterWorldAccepted, sessionState, feedback)
        {
        }

        public CharacterSelectPresenter(
            ICharacterSelectView view,
            ICharacterSelectService service,
            Action<EnterWorldResultDto, CharacterSummaryDto> onEnterWorldAccepted)
            : this(view, WrapRuntimeService(service), onEnterWorldAccepted)
        {
        }

        public CharacterSelectPresenter(
            ICharacterSelectView view,
            ICharacterSelectService service,
            Action<EnterWorldResultDto> onEnterWorldAccepted)
            : this(view, service, (result, _) => onEnterWorldAccepted?.Invoke(result))
        {
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
            _service.StateChanged += HandleServiceStateChanged;

            _sessionState?.TryTransitionTo(ClientSessionState.CharacterSelection);
            _feedback?.ShowLoading("Loading character list...");

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
            _service.StateChanged -= HandleServiceStateChanged;
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
            _view.RenderCharacters(BuildViewData(_characters), _selectedCharacterId);
            _view.SetActionAvailability(_selectedCharacterId > 0);

            CharacterSummaryDto selected = FindCharacter(_selectedCharacterId);
            string name = selected != null ? selected.name : "Unknown";
            _view.SetSelectedCharacterDetails(ToViewData(selected));
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
                _feedback?.ShowError("Please select a character.");
                return;
            }

            await RunBusyAsync(async () =>
            {
                _feedback?.ShowLoading("Preparing world entry...");
                _view.SetStatus("Entering world...");

                EnterWorldResultDto result = await _service.EnterWorldAsync(_selectedCharacterId);
                if (!result.success)
                {
                    string errorMsg = $"Enter world failed: {result.message}";
                    _view.SetStatus(errorMsg);
                    _feedback?.ShowError(errorMsg);
                    Debug.LogWarning($"[CharacterSelectPresenter] {errorMsg}");
                    return;
                }

                _feedback?.Clear();
                _sessionState?.TryTransitionTo(ClientSessionState.EnteringWorld);
                Debug.Log($"[CharacterSelectPresenter] Enter world accepted for character={result.characterId}, scene={result.sceneName}.");
                _onEnterWorldAccepted?.Invoke(result, FindCharacter(_selectedCharacterId));
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

                _view.RenderCharacters(BuildViewData(_characters), _selectedCharacterId);
                _view.SetActionAvailability(_selectedCharacterId > 0);
                _view.HideDeleteConfirmation();
                _view.SetSelectedCharacterDetails(ToViewData(FindCharacter(_selectedCharacterId)));
                _view.SetStatus($"Loaded {_characters.Count} character(s).");

                Debug.Log($"[CharacterSelectPresenter] Loaded {_characters.Count} character(s).");
            });
        }

        private async Task RunBusyAsync(Func<Task> operation)
        {
            if (_isBusy) return;

            _isBusy = true;
            _view.SetBusy(true);
            _view.SetLoading(true, false);

            try
            {
                await operation();
            }
            finally
            {
                _isBusy = false;
                _view.SetBusy(false);
                _view.SetLoading(false, false);
                _view.SetActionAvailability(_selectedCharacterId > 0);
            }
        }

        private void HandleServiceStateChanged(CharacterSelectServiceState state, string message)
        {
            bool loading = state == CharacterSelectServiceState.Loading || state == CharacterSelectServiceState.Reconnecting;
            bool reconnecting = state == CharacterSelectServiceState.Reconnecting;
            _view.SetLoading(loading, reconnecting);

            if (state == CharacterSelectServiceState.Failed)
                _feedback?.ShowError(message);
            else if (loading)
                _feedback?.ShowLoading(message, reconnecting);
            else if (state == CharacterSelectServiceState.Ready)
                _feedback?.Clear();

            if (!string.IsNullOrWhiteSpace(message))
                _view.SetStatus(message);
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

        private static ICharacterSelectRuntimeService WrapRuntimeService(ICharacterSelectService service)
        {
            if (service is ICharacterSelectRuntimeService runtime)
                return runtime;

            return new CharacterSelectService(null, service ?? new MockCharacterSelectService());
        }

        private static IReadOnlyList<CharacterSummaryViewData> BuildViewData(IReadOnlyList<CharacterSummaryDto> characters)
        {
            var list = new List<CharacterSummaryViewData>(characters != null ? characters.Count : 0);
            if (characters == null)
                return list;

            for (int i = 0; i < characters.Count; i++)
            {
                CharacterSummaryDto dto = characters[i];
                if (dto == null)
                    continue;

                list.Add(ToViewData(dto).Value);
            }

            return list;
        }

        private static CharacterSummaryViewData? ToViewData(CharacterSummaryDto dto)
        {
            if (dto == null)
                return null;

            return new CharacterSummaryViewData(
                dto.characterId,
                dto.name,
                dto.classId,
                dto.level,
                dto.powerScore,
                dto.mapId,
                dto.mapName,
                dto.isLastPlayed);
        }
    }
}
