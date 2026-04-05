using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MuLike.Server.Auth
{
    public sealed class AccountRecord
    {
        public int AccountId { get; set; }
        public string Username { get; set; }
        public string AccountName { get; set; }
        public string PasswordHash { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public sealed class SessionRecord
    {
        public string SessionId { get; set; }
        public int AccountId { get; set; }
        public string RefreshTokenHash { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? RevokedAtUtc { get; set; }
        public string RevokedReason { get; set; }
        public string ReplacedBySessionId { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }

        public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAtUtc;
        public bool IsRevoked => RevokedAtUtc.HasValue;
    }

    public interface IAccountStore
    {
        bool TryGetByUsername(string username, out AccountRecord account);
        bool TryGetByAccountId(int accountId, out AccountRecord account);
        void Upsert(AccountRecord account);
    }

    public interface ISessionStore
    {
        void Upsert(SessionRecord session);
        bool TryGetBySessionId(string sessionId, out SessionRecord session);
        bool TryGetByRefreshTokenHash(string refreshTokenHash, out SessionRecord session);
        IReadOnlyList<SessionRecord> GetByAccountId(int accountId);
    }

    public sealed class InMemoryAccountStore : IAccountStore
    {
        private readonly ConcurrentDictionary<int, AccountRecord> _byId = new();
        private readonly ConcurrentDictionary<string, int> _idByUsername = new(StringComparer.OrdinalIgnoreCase);

        public InMemoryAccountStore(IEnumerable<AccountRecord> seedAccounts = null)
        {
            if (seedAccounts == null)
                return;

            foreach (AccountRecord account in seedAccounts)
                Upsert(account);
        }

        public bool TryGetByUsername(string username, out AccountRecord account)
        {
            account = null;
            if (string.IsNullOrWhiteSpace(username))
                return false;

            if (!_idByUsername.TryGetValue(username, out int id))
                return false;

            return _byId.TryGetValue(id, out account);
        }

        public bool TryGetByAccountId(int accountId, out AccountRecord account)
        {
            return _byId.TryGetValue(accountId, out account);
        }

        public void Upsert(AccountRecord account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            if (string.IsNullOrWhiteSpace(account.Username)) throw new ArgumentException("Username is required.", nameof(account));
            if (string.IsNullOrWhiteSpace(account.PasswordHash)) throw new ArgumentException("PasswordHash is required.", nameof(account));

            _byId[account.AccountId] = account;
            _idByUsername[account.Username] = account.AccountId;
        }
    }

    public sealed class InMemorySessionStore : ISessionStore
    {
        private readonly ConcurrentDictionary<string, SessionRecord> _bySessionId = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string> _sessionIdByRefreshHash = new(StringComparer.Ordinal);

        public void Upsert(SessionRecord session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrWhiteSpace(session.SessionId)) throw new ArgumentException("SessionId is required.", nameof(session));
            if (string.IsNullOrWhiteSpace(session.RefreshTokenHash)) throw new ArgumentException("RefreshTokenHash is required.", nameof(session));

            _bySessionId[session.SessionId] = session;
            _sessionIdByRefreshHash[session.RefreshTokenHash] = session.SessionId;
        }

        public bool TryGetBySessionId(string sessionId, out SessionRecord session)
        {
            session = null;
            if (string.IsNullOrWhiteSpace(sessionId))
                return false;

            return _bySessionId.TryGetValue(sessionId, out session);
        }

        public bool TryGetByRefreshTokenHash(string refreshTokenHash, out SessionRecord session)
        {
            session = null;
            if (string.IsNullOrWhiteSpace(refreshTokenHash))
                return false;

            if (!_sessionIdByRefreshHash.TryGetValue(refreshTokenHash, out string sessionId))
                return false;

            return _bySessionId.TryGetValue(sessionId, out session);
        }

        public IReadOnlyList<SessionRecord> GetByAccountId(int accountId)
        {
            return _bySessionId.Values
                .Where(s => s.AccountId == accountId)
                .OrderByDescending(s => s.CreatedAtUtc)
                .ToArray();
        }
    }

    public sealed class AuthenticationTokens
    {
        public string AccessToken { get; set; }
        public DateTime AccessTokenExpiresAtUtc { get; set; }
        public string RefreshToken { get; set; }
        public DateTime RefreshTokenExpiresAtUtc { get; set; }
    }

    public sealed class AuthenticationResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public AccountRecord Account { get; set; }
        public string SessionId { get; set; }
        public AuthenticationTokens Tokens { get; set; }

        public static AuthenticationResult Fail(string error)
        {
            return new AuthenticationResult { Success = false, Error = error ?? "Authentication failed." };
        }
    }

    public sealed class AuthService
    {
        private readonly IAccountStore _accounts;
        private readonly ISessionStore _sessions;
        private readonly PasswordHasher _passwordHasher;
        private readonly TokenService _tokenService;
        private readonly AuthOptions _options;

        public AuthService(
            IAccountStore accountStore,
            ISessionStore sessionStore,
            PasswordHasher passwordHasher,
            TokenService tokenService,
            AuthOptions options)
        {
            _accounts = accountStore ?? throw new ArgumentNullException(nameof(accountStore));
            _sessions = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public AuthenticationResult Authenticate(string username, string password, string ipAddress = null, string userAgent = null)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return AuthenticationResult.Fail("Username and password are required.");

            if (!_accounts.TryGetByUsername(username, out AccountRecord account) || account == null || !account.IsActive)
                return AuthenticationResult.Fail("Invalid credentials.");

            PasswordVerificationResult verification = _passwordHasher.VerifyDetailed(password, account.PasswordHash);
            if (!verification.Success)
                return AuthenticationResult.Fail("Invalid credentials.");

            if (verification.RequiresRehash)
            {
                _accounts.Upsert(new AccountRecord
                {
                    AccountId = account.AccountId,
                    Username = account.Username,
                    AccountName = account.AccountName,
                    PasswordHash = _passwordHasher.Hash(password),
                    IsActive = account.IsActive
                });
            }

            SessionRecord session = CreateSession(account, ipAddress, userAgent);
            AuthenticationTokens tokens = CreateTokenPair(account, session);
            TrimExcessActiveSessions(account.AccountId);

            return new AuthenticationResult
            {
                Success = true,
                Error = string.Empty,
                Account = account,
                SessionId = session.SessionId,
                Tokens = tokens
            };
        }

        public AuthenticationResult Refresh(string refreshToken, string ipAddress = null, string userAgent = null)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return AuthenticationResult.Fail("Refresh token required.");

            string refreshHash = _tokenService.HashRefreshToken(refreshToken);
            if (!_sessions.TryGetByRefreshTokenHash(refreshHash, out SessionRecord currentSession) || currentSession == null)
                return AuthenticationResult.Fail("Invalid refresh token.");

            DateTime now = DateTime.UtcNow;
            if (currentSession.IsRevoked)
                return AuthenticationResult.Fail("Refresh token revoked.");

            if (currentSession.IsExpired(now))
                return AuthenticationResult.Fail("Refresh token expired.");

            if (!_accounts.TryGetByAccountId(currentSession.AccountId, out AccountRecord account) || account == null || !account.IsActive)
                return AuthenticationResult.Fail("Account not available.");

            SessionRecord effectiveSession = currentSession;

            if (_options.RotateRefreshTokens)
            {
                SessionRecord rotated = CreateSession(account, ipAddress, userAgent);
                RevokeSession(currentSession.SessionId, "rotated", rotated.SessionId);
                effectiveSession = rotated;

                if (!_sessions.TryGetBySessionId(rotated.SessionId, out SessionRecord rotatedStored))
                    return AuthenticationResult.Fail("Session store error.");

                effectiveSession = rotatedStored;
            }

            string accessToken = _tokenService.CreateAccessToken(account.AccountId, account.AccountName, effectiveSession.SessionId);
            DateTime accessExp = now.Add(_options.AccessTokenLifetime);

            string rawRefreshToken;
            if (_options.RotateRefreshTokens)
            {
                // For rotated sessions, raw token cannot be recovered from hash, so create a new token pair now.
                RefreshTokenValue newRefresh = _tokenService.CreateRefreshToken();
                _sessions.Upsert(new SessionRecord
                {
                    SessionId = effectiveSession.SessionId,
                    AccountId = effectiveSession.AccountId,
                    RefreshTokenHash = newRefresh.Hash,
                    CreatedAtUtc = effectiveSession.CreatedAtUtc,
                    ExpiresAtUtc = effectiveSession.ExpiresAtUtc,
                    RevokedAtUtc = effectiveSession.RevokedAtUtc,
                    RevokedReason = effectiveSession.RevokedReason,
                    ReplacedBySessionId = effectiveSession.ReplacedBySessionId,
                    IpAddress = effectiveSession.IpAddress,
                    UserAgent = effectiveSession.UserAgent
                });
                rawRefreshToken = newRefresh.Raw;
            }
            else
            {
                rawRefreshToken = refreshToken;
            }

            return new AuthenticationResult
            {
                Success = true,
                Error = string.Empty,
                Account = account,
                SessionId = effectiveSession.SessionId,
                Tokens = new AuthenticationTokens
                {
                    AccessToken = accessToken,
                    AccessTokenExpiresAtUtc = accessExp,
                    RefreshToken = rawRefreshToken,
                    RefreshTokenExpiresAtUtc = effectiveSession.ExpiresAtUtc
                }
            };
        }

        public bool RevokeRefreshToken(string refreshToken, string reason = "manual")
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return false;

            string hash = _tokenService.HashRefreshToken(refreshToken);
            if (!_sessions.TryGetByRefreshTokenHash(hash, out SessionRecord session) || session == null)
                return false;

            return RevokeSession(session.SessionId, reason, null);
        }

        public int RevokeAllSessions(int accountId, string reason = "manual")
        {
            IReadOnlyList<SessionRecord> sessions = _sessions.GetByAccountId(accountId);
            int revoked = 0;
            for (int i = 0; i < sessions.Count; i++)
            {
                if (sessions[i].IsRevoked)
                    continue;

                if (RevokeSession(sessions[i].SessionId, reason, null))
                    revoked++;
            }

            return revoked;
        }

        public bool TryValidateAccessToken(string accessToken, out AccessTokenPrincipal principal)
        {
            AccessTokenValidationResult result = _tokenService.ValidateAccessToken(accessToken);
            principal = result.Principal;
            return result.Success;
        }

        private SessionRecord CreateSession(AccountRecord account, string ipAddress, string userAgent)
        {
            DateTime now = DateTime.UtcNow;
            RefreshTokenValue refresh = _tokenService.CreateRefreshToken();
            var session = new SessionRecord
            {
                SessionId = Guid.NewGuid().ToString("N"),
                AccountId = account.AccountId,
                RefreshTokenHash = refresh.Hash,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(_options.RefreshTokenLifetime),
                RevokedAtUtc = null,
                RevokedReason = null,
                ReplacedBySessionId = null,
                IpAddress = ipAddress ?? string.Empty,
                UserAgent = userAgent ?? string.Empty
            };

            _sessions.Upsert(session);
            return session;
        }

        private AuthenticationTokens CreateTokenPair(AccountRecord account, SessionRecord session)
        {
            DateTime now = DateTime.UtcNow;
            string access = _tokenService.CreateAccessToken(account.AccountId, account.AccountName, session.SessionId);

            RefreshTokenValue refresh = _tokenService.CreateRefreshToken();
            _sessions.Upsert(new SessionRecord
            {
                SessionId = session.SessionId,
                AccountId = session.AccountId,
                RefreshTokenHash = refresh.Hash,
                CreatedAtUtc = session.CreatedAtUtc,
                ExpiresAtUtc = session.ExpiresAtUtc,
                RevokedAtUtc = session.RevokedAtUtc,
                RevokedReason = session.RevokedReason,
                ReplacedBySessionId = session.ReplacedBySessionId,
                IpAddress = session.IpAddress,
                UserAgent = session.UserAgent
            });

            return new AuthenticationTokens
            {
                AccessToken = access,
                AccessTokenExpiresAtUtc = now.Add(_options.AccessTokenLifetime),
                RefreshToken = refresh.Raw,
                RefreshTokenExpiresAtUtc = session.ExpiresAtUtc
            };
        }

        private void TrimExcessActiveSessions(int accountId)
        {
            IReadOnlyList<SessionRecord> sessions = _sessions.GetByAccountId(accountId);
            var active = sessions
                .Where(s => !s.IsRevoked && !s.IsExpired(DateTime.UtcNow))
                .OrderByDescending(s => s.CreatedAtUtc)
                .ToArray();

            for (int i = _options.MaxActiveRefreshSessionsPerAccount; i < active.Length; i++)
            {
                RevokeSession(active[i].SessionId, "session_limit", null);
            }
        }

        private bool RevokeSession(string sessionId, string reason, string replacedBySessionId)
        {
            if (!_sessions.TryGetBySessionId(sessionId, out SessionRecord session) || session == null)
                return false;

            if (session.IsRevoked)
                return true;

            _sessions.Upsert(new SessionRecord
            {
                SessionId = session.SessionId,
                AccountId = session.AccountId,
                RefreshTokenHash = session.RefreshTokenHash,
                CreatedAtUtc = session.CreatedAtUtc,
                ExpiresAtUtc = session.ExpiresAtUtc,
                RevokedAtUtc = DateTime.UtcNow,
                RevokedReason = reason ?? "revoked",
                ReplacedBySessionId = replacedBySessionId,
                IpAddress = session.IpAddress,
                UserAgent = session.UserAgent
            });

            return true;
        }
    }
}
