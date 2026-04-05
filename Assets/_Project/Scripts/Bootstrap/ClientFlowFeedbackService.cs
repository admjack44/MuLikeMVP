using System;
using UnityEngine;

namespace MuLike.Bootstrap
{
    /// <summary>
    /// Feedback service for client-side UI messaging during login/character select flows.
    /// Decouples state transitions from UI rendering, managing loading spinners and error display.
    /// </summary>
    public sealed class ClientFlowFeedbackService
    {
        public enum FeedbackState
        {
            Idle,
            Loading,
            Error,
        }

        public FeedbackState CurrentState { get; private set; } = FeedbackState.Idle;
        public string CurrentMessage { get; private set; } = string.Empty;
        public bool IsLoading => CurrentState == FeedbackState.Loading;
        public bool IsError => CurrentState == FeedbackState.Error;

        public event Action<FeedbackState, string> FeedbackChanged;

        /// <summary>
        /// Show loading state with optional message and reconnection flag.
        /// </summary>
        public void ShowLoading(string message = "Loading...", bool isReconnecting = false)
        {
            string fullMessage = message ?? "Loading...";
            if (isReconnecting)
                fullMessage += " (reconnecting...)";

            SetFeedback(FeedbackState.Loading, fullMessage);
        }

        /// <summary>
        /// Show error state with message. Auto-dismisses after configurable delay.
        /// </summary>
        public void ShowError(string message)
        {
            SetFeedback(FeedbackState.Error, message ?? "An error occurred.");
        }

        /// <summary>
        /// Clear feedback and return to idle state.
        /// </summary>
        public void Clear()
        {
            SetFeedback(FeedbackState.Idle, string.Empty);
        }

        private void SetFeedback(FeedbackState state, string message)
        {
            if (CurrentState == state && string.Equals(CurrentMessage, message, StringComparison.Ordinal))
                return;

            CurrentState = state;
            CurrentMessage = message ?? string.Empty;
            FeedbackChanged?.Invoke(CurrentState, CurrentMessage);

            if (state == FeedbackState.Error)
                Debug.LogWarning($"[ClientFlowFeedback] Error: {message}");
        }
    }
}
