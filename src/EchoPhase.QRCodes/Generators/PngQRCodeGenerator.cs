// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Mime;
using QRCoder;

namespace EchoPhase.QRCodes.Generators
{
    public class PngQRCodeGenerator : IQRCodeGenerator
    {
        public static string Format = MediaTypeNames.Image.Png;
        private int _size = 14;

        public PngQRCodeGenerator()
        {
        }

        public PngQRCodeGenerator WithSize(int size)
        {
            _size = size;
            return this;
        }

        public string Generate(string content)
        {
            return string.Format("data:image/png;base64,{0}", GenerateBase64(content));
        }

        public string GenerateBase64(string content)
        {
            byte[] pngBytes = GenerateBytes(content);

            return Convert.ToBase64String(pngBytes);
        }

        public byte[] GenerateBytes(string content)
        {
            using QRCodeGenerator qrGenerator = new();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);

            using PngByteQRCode qrCode = new(qrCodeData);
            return qrCode.GetGraphic(_size);
        }
    }
}
