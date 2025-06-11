using System.Security.Cryptography;
using System.Text;
using EchoPhase.Extensions;
using EchoPhase.Models;
using EchoPhase.Settings;
using Isopoh.Cryptography.Argon2;
using Isopoh.Cryptography.SecureArray;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EchoPhase.Hashers
{
    public class Argon2Hasher : IPasswordHasher<User>
    {
        private readonly int _threads = (1 + Environment.ProcessorCount) / 2;

        private readonly byte[] _key;
        private readonly Argon2Config _config;

        public Argon2Hasher(IOptions<Argon2Settings> settings)
        {
            _key = Convert.FromBase64String(settings.Value.Secret);

            _config = new Argon2Config
            {
                Type = Argon2Type.DataIndependentAddressing,
                Version = Argon2Version.Nineteen,
                TimeCost = 6,
                MemoryCost = 1 << 16,
                Lanes = 4,
                Threads = _threads > 4 ? 4 : _threads,
                HashLength = 32,
                Secret = _key
            };
        }

        public string HashPassword(User user, string password)
        {
            var config = _config.Clone();

            config.Salt = GenerateSalt();
            config.Password = Encoding.UTF8.GetBytes(password);

            var argon2 = new Argon2(config);
            string hashString;
            using (SecureArray<byte> hash = argon2.Hash())
            {
                hashString = config.EncodeString(hash.Buffer);
            }

            return hashString;
        }

        public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
        {
            var config = _config.Clone();

            config.Password = Encoding.UTF8.GetBytes(providedPassword);

            SecureArray<byte>? expectedHash = null;
            SecureArray<byte>? actualHash = null;
            try
            {
                if (!config.DecodeString(hashedPassword, out expectedHash) || expectedHash == null)
                    return PasswordVerificationResult.Failed;

                var argon2 = new Argon2(config);
                actualHash = argon2.Hash();

                return Argon2.FixedTimeEquals(expectedHash, actualHash)
                    ? PasswordVerificationResult.Success
                    : PasswordVerificationResult.Failed;
            }
            finally
            {
                expectedHash?.Dispose();
                actualHash?.Dispose();
            }
        }

        private byte[] GenerateSalt()
        {
            var salt = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return salt;
        }
    }
}
