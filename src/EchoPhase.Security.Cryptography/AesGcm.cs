using System.Security.Cryptography;
using System.Text;
using EchoPhase.Configuration.Settings;
using EchoPhase.Types.Result.Extensions;
using Microsoft.Extensions.Options;
using SystemAesGcm = System.Security.Cryptography.AesGcm;

namespace EchoPhase.Security.Cryptography
{
    public class AesGcm
    {
        private readonly AesSettings _settings;
        private byte[] _key;

        public AesGcm(
            IOptions<AesSettings> settings,
            IKeyVault keyVault
        )
        {
            _settings = settings.Value;

            var result = keyVault.GetOrSet(_settings.Key);

            result.OnFailure(err =>
                throw new InvalidOperationException(err.Value));

            if (!result.TryGetValue(out var key))
                throw new InvalidOperationException($"Missing '{_settings.Key}' key.");

            _key = key;
        }

        public AesGcm WithKey(byte[] key)
        {
            _key = key;
            return this;
        }

        public string EncryptToBase64(string plainText) =>
            EncryptToBase64(Encoding.UTF8.GetBytes(plainText));

        public string EncryptToBase64(byte[] bytes) =>
            Convert.ToBase64String(Encrypt(bytes));

        public byte[] Encrypt(string plainText) =>
            Encrypt(Encoding.UTF8.GetBytes(plainText));

        public byte[] Encrypt(byte[] bytes)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(_settings.NonceSize);
            byte[] ciphertext = new byte[bytes.Length];
            byte[] tag = new byte[_settings.TagSize];

            using var aesGcm = new SystemAesGcm(_key, _settings.TagSize);
            aesGcm.Encrypt(nonce, bytes, ciphertext, tag);

            byte[] result = new byte[_settings.NonceSize + ciphertext.Length + _settings.TagSize];
            Buffer.BlockCopy(nonce, 0, result, 0, _settings.NonceSize);
            Buffer.BlockCopy(ciphertext, 0, result, _settings.NonceSize, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, _settings.NonceSize + ciphertext.Length, _settings.TagSize);

            return result;
        }

        public string DecryptToBase64(string encrypted) =>
            DecryptToBase64(Encoding.UTF8.GetBytes(encrypted));

        public string DecryptToBase64(byte[] bytes) =>
            Encoding.UTF8.GetString(Decrypt(bytes));

        public byte[] Decrypt(string encrypted) =>
            Decrypt(Convert.FromBase64String(encrypted));

        public byte[] Decrypt(byte[] bytes)
        {
            byte[] fullCipher = bytes;

            byte[] nonce = new byte[_settings.NonceSize];
            byte[] tag = new byte[_settings.TagSize];
            byte[] ciphertext = new byte[fullCipher.Length - _settings.NonceSize - _settings.TagSize];

            Buffer.BlockCopy(fullCipher, 0, nonce, 0, _settings.NonceSize);
            Buffer.BlockCopy(fullCipher, _settings.NonceSize, ciphertext, 0, ciphertext.Length);
            Buffer.BlockCopy(fullCipher, _settings.NonceSize + ciphertext.Length, tag, 0, _settings.TagSize);

            byte[] decryptedBytes = new byte[ciphertext.Length];

            using var aesGcm = new SystemAesGcm(_key, _settings.TagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, decryptedBytes);

            return decryptedBytes;
        }
    }
}
