using System.Security.Cryptography;
using System.Text;
using EchoPhase.Interfaces;
using EchoPhase.Settings;
using Microsoft.Extensions.Options;

namespace EchoPhase.Services.Security
{
    public class AesService
    {
        private readonly AesSettings _settings;
        private readonly IKeysService _keysService;

        public AesService(
            IOptions<AesSettings> settings,
            IKeysService keysService
        )
        {
            _settings = settings.Value;
            _keysService = keysService;
        }

        public string Encrypt(string plainText)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(_settings.NonceSize);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[_settings.TagSize];
            byte[] key = Convert.FromBase64String(_keysService.GetOrSet(_settings.Key));

            using var aesGcm = new AesGcm(key, _settings.TagSize);
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            byte[] result = new byte[_settings.NonceSize + ciphertext.Length + _settings.TagSize];
            Buffer.BlockCopy(nonce, 0, result, 0, _settings.NonceSize);
            Buffer.BlockCopy(ciphertext, 0, result, _settings.NonceSize, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, _settings.NonceSize + ciphertext.Length, _settings.TagSize);

            return Convert.ToBase64String(result);
        }

        public string Decrypt(string encrypted)
        {
            byte[] fullCipher = Convert.FromBase64String(encrypted);

            byte[] nonce = new byte[_settings.NonceSize];
            byte[] tag = new byte[_settings.TagSize];
            byte[] ciphertext = new byte[fullCipher.Length - _settings.NonceSize - _settings.TagSize];

            Buffer.BlockCopy(fullCipher, 0, nonce, 0, _settings.NonceSize);
            Buffer.BlockCopy(fullCipher, _settings.NonceSize, ciphertext, 0, ciphertext.Length);
            Buffer.BlockCopy(fullCipher, _settings.NonceSize + ciphertext.Length, tag, 0, _settings.TagSize);

            byte[] decryptedBytes = new byte[ciphertext.Length];
            byte[] key = Convert.FromBase64String(_keysService.GetOrSet(_settings.Key));

            using var aesGcm = new AesGcm(key, _settings.TagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, decryptedBytes);

            return Encoding.UTF8.GetString(decryptedBytes);
        }
    }
}
