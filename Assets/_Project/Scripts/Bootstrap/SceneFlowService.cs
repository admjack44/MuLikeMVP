using System;
using MuLike.Core;
using UnityEngine;

namespace MuLike.Bootstrap
{
    /// <summary>
    /// Runtime scene flow facade for Boot/Login/CharacterSelect/World transitions.
    /// </summary>
    public sealed class SceneFlowService
    {
        private readonly SceneController _sceneController;
        private readonly FrontendFlowDirector _flowDirector;
        private readonly SessionStateClient _sessionState;

        public SceneFlowService(
            SceneController sceneController,
            FrontendFlowDirector flowDirector,
            SessionStateClient sessionState)
        {
            _sceneController = sceneController;
            _flowDirector = flowDirector;
            _sessionState = sessionState;
        }

        public void EnterBoot()
        {
            Debug.Log("[SceneFlowService] EnterBoot requested.");
            _flowDirector?.EnterBoot();
        }

        public void EnterLogin()
        {
            Debug.Log("[SceneFlowService] EnterLogin requested.");
            _flowDirector?.EnterLogin();
        }

        public void EnterCharacterSelect()
        {
            if (_sessionState != null && !_sessionState.IsAuthenticated)
            {
                Debug.LogWarning("[SceneFlowService] CharacterSelect blocked: session is not authenticated.");
                _flowDirector?.EnterLogin();
                return;
            }

            Debug.Log("[SceneFlowService] EnterCharacterSelect requested.");
            _flowDirector?.EnterCharacterSelect();
        }

        public void EnterWorld(string sceneName)
        {
            if (_sessionState != null && !_sessionState.IsAuthenticated)
            {
                Debug.LogWarning("[SceneFlowService] EnterWorld blocked: session is not authenticated.");
                _flowDirector?.EnterLogin();
                return;
            }

            string chosen = string.IsNullOrWhiteSpace(sceneName)
                ? (_sessionState != null ? _sessionState.CurrentWorldScene : string.Empty)
                : sceneName;

            if (_sessionState != null)
                _sessionState.SetWorldScene(chosen);

            Debug.Log($"[SceneFlowService] EnterWorld requested. Scene='{chosen}'.");
            _flowDirector?.EnterWorld(chosen);
        }

        public void LogoutToLogin()
        {
            Debug.Log("[SceneFlowService] LogoutToLogin requested.");
            _sessionState?.ClearForLogout();
            _flowDirector?.LogoutToLogin();
        }

        public bool TryLoadSceneDirect(string sceneName, string fallbackSceneName = "Login")
        {
            if (_sceneController == null)
            {
                Debug.LogError("[SceneFlowService] SceneController is null. Cannot load scene.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[SceneFlowService] Scene name is empty. Direct load aborted.");
                return false;
            }

            _sceneController.LoadScene(sceneName, fallbackSceneName);
            return true;
        }
    }
}