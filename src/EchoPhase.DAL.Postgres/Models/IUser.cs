namespace EchoPhase.DAL.Postgres.Models
{
    public interface IUser : ITrackingEntity
    {
        Guid Id
        {
            get; set;
        }
        string Name
        {
            get; set;
        }
        string? UserName
        {
            get; set;
        }
        string? NormalizedUserName
        {
            get; set;
        }
        string? Email
        {
            get; set;
        }
        string? NormalizedEmail
        {
            get; set;
        }
        bool EmailConfirmed
        {
            get; set;
        }
        string? PasswordHash
        {
            get; set;
        }
        string? SecurityStamp
        {
            get; set;
        }
        string? ConcurrencyStamp
        {
            get; set;
        }
        string? PhoneNumber
        {
            get; set;
        }
        bool PhoneNumberConfirmed
        {
            get; set;
        }
        bool TwoFactorEnabled
        {
            get; set;
        }
        DateTimeOffset? LockoutEnd
        {
            get; set;
        }
        bool LockoutEnabled
        {
            get; set;
        }
        int AccessFailedCount
        {
            get; set;
        }
        string? ProfileImageName
        {
            get; set;
        }
    }
}
