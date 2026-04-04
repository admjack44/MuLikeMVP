namespace MuLike.Server.Auth
{
    public sealed class AuthService
    {
        private readonly PasswordHasher _passwordHasher;
        private readonly TokenService _tokenService;

        public AuthService(PasswordHasher passwordHasher, TokenService tokenService)
        {
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
        }

        public bool ValidateCredentials(string providedPassword, string storedHash)
        {
            return _passwordHasher.Verify(providedPassword, storedHash);
        }

        public string IssueAccessToken(int accountId, string accountName)
        {
            return _tokenService.CreateToken(accountId, accountName);
        }
    }
}
