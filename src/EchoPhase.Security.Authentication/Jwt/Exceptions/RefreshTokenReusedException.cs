namespace EchoPhase.Security.Authentication.Jwt.Exceptions
{
    public class RefreshTokenReusedException : UnauthorizedAccessException
    {
        public Guid UserId { get; }

        public RefreshTokenReusedException(Guid userId)
            : base("Refresh token reuse detected.")
        {
            UserId = userId;
        }
    }
}
