using System;
using System.Collections.Generic;

namespace MuLike.UI.Login
{
    /// <summary>
    /// Minimal explicit state machine for login lifecycle transitions.
    /// </summary>
    public sealed class LoginFlowStateMachine
    {
        private static readonly Dictionary<LoginFlowState, LoginFlowState[]> AllowedTransitions = new()
        {
            [LoginFlowState.Idle] = new[]
            {
                LoginFlowState.Connecting,
                LoginFlowState.Authenticating,
                LoginFlowState.Refreshing,
                LoginFlowState.LoggedOut,
                LoginFlowState.Failed
            },
            [LoginFlowState.Connecting] = new[]
            {
                LoginFlowState.Authenticating,
                LoginFlowState.Refreshing,
                LoginFlowState.Failed,
                LoginFlowState.LoggedOut,
                LoginFlowState.Idle
            },
            [LoginFlowState.Authenticating] = new[]
            {
                LoginFlowState.Authenticated,
                LoginFlowState.Failed,
                LoginFlowState.LoggedOut,
                LoginFlowState.Idle
            },
            [LoginFlowState.Authenticated] = new[]
            {
                LoginFlowState.Refreshing,
                LoginFlowState.LoggedOut,
                LoginFlowState.Failed,
                LoginFlowState.Idle
            },
            [LoginFlowState.Refreshing] = new[]
            {
                LoginFlowState.Authenticated,
                LoginFlowState.Failed,
                LoginFlowState.LoggedOut,
                LoginFlowState.Idle
            },
            [LoginFlowState.Failed] = new[]
            {
                LoginFlowState.Idle,
                LoginFlowState.Connecting,
                LoginFlowState.Authenticating,
                LoginFlowState.Refreshing,
                LoginFlowState.LoggedOut
            },
            [LoginFlowState.LoggedOut] = new[]
            {
                LoginFlowState.Idle,
                LoginFlowState.Connecting,
                LoginFlowState.Authenticating
            }
        };

        public LoginFlowState Current { get; private set; } = LoginFlowState.Idle;

        public event Action<LoginFlowState> StateChanged;

        public bool TryMoveTo(LoginFlowState next)
        {
            if (Current == next)
                return true;

            if (!AllowedTransitions.TryGetValue(Current, out LoginFlowState[] allowed))
                return false;

            for (int i = 0; i < allowed.Length; i++)
            {
                if (allowed[i] != next)
                    continue;

                Current = next;
                StateChanged?.Invoke(Current);
                return true;
            }

            return false;
        }
    }
}
