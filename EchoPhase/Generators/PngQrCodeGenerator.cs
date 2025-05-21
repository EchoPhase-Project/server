using System.Net.Mime;

using QRCoder;

using EchoPhase.Interfaces;
using EchoPhase.Attributes;

namespace EchoPhase.Generators
{
	[QrFormat(MediaTypeNames.Image.Png)]
	public class PngQrCodeGenerator : IQrCodeGenerator
	{
		private int _size = 14;

		public PngQrCodeGenerator() {}

		public PngQrCodeGenerator WithSize(int size)
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
			using QRCodeGenerator qrGenerator = new QRCodeGenerator();
			QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);

			using PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
			return qrCode.GetGraphic(_size);
		}
	}
}
