using Isopoh.Cryptography.Argon2;

namespace EchoPhase.Security.Hashers.Extensions
{
    internal static class Argon2ConfigExtensions
    {
        public static Argon2Config Clone(this Argon2Config original)
        {
            return new Argon2Config
            {
                Type = original.Type,
                Version = original.Version,
                TimeCost = original.TimeCost,
                MemoryCost = original.MemoryCost,
                Lanes = original.Lanes,
                Threads = original.Threads,
                HashLength = original.HashLength,
                Secret = original.Secret,
            };
        }
    }
}
