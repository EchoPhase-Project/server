using System.Security.Cryptography;
using System.Text;
using EchoPhase.Settings;
using Microsoft.Extensions.Options;

namespace EchoPhase.Services.Security
{
    public class AesService
    {
        private readonly AesSettings _aesSettings;
        private readonly byte[] _key;
        private readonly int _tagSize = 16;
        private readonly int _nonceSize = 12;

        public AesService(IOptions<AesSettings> aesSettings)
        {
            _aesSettings = aesSettings.Value;
            _key = Convert.FromBase64String(_aesSettings.Secret);
        }

        public string Encrypt(string plainText)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(_nonceSize);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[_tagSize];

            using var aesGcm = new AesGcm(_key, _tagSize);
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            byte[] result = new byte[_nonceSize + ciphertext.Length + _tagSize];
            Buffer.BlockCopy(nonce, 0, result, 0, _nonceSize);
            Buffer.BlockCopy(ciphertext, 0, result, _nonceSize, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, _nonceSize + ciphertext.Length, _tagSize);

            return Convert.ToBase64String(result);
        }

        public string Decrypt(string encrypted)
        {
            byte[] fullCipher = Convert.FromBase64String(encrypted);

            byte[] nonce = new byte[_nonceSize];
            byte[] tag = new byte[_tagSize];
            byte[] ciphertext = new byte[fullCipher.Length - _nonceSize - _tagSize];

            Buffer.BlockCopy(fullCipher, 0, nonce, 0, _nonceSize);
            Buffer.BlockCopy(fullCipher, _nonceSize, ciphertext, 0, ciphertext.Length);
            Buffer.BlockCopy(fullCipher, _nonceSize + ciphertext.Length, tag, 0, _tagSize);

            byte[] decryptedBytes = new byte[ciphertext.Length];

            using var aesGcm = new AesGcm(_key, _tagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, decryptedBytes);

            return Encoding.UTF8.GetString(decryptedBytes);
        }
    }
}
