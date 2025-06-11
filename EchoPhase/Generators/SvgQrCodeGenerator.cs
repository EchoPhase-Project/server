using System.Drawing;
using System.Net.Mime;
using System.Text.RegularExpressions;
using EchoPhase.Attributes;
using EchoPhase.Interfaces;
using QRCoder;

namespace EchoPhase.Generators
{
    [QrFormat(MediaTypeNames.Image.Svg)]
    public class SvgQrCodeGenerator : IQrCodeGenerator
    {
        private int _size = 14;
        private HashSet<string> _cssClasses = new();

        public SvgQrCodeGenerator()
        {
        }

        public SvgQrCodeGenerator WithSize(int size)
        {
            _size = size;
            return this;
        }

        public SvgQrCodeGenerator AddClasses(params string[] classes)
        {
            foreach (var cls in classes)
            {
                if (!string.IsNullOrWhiteSpace(cls))
                    _cssClasses.Add(cls);
            }

            return this;
        }

        public SvgQrCodeGenerator RemoveClasses(params string[] classes)
        {
            foreach (var cls in classes)
            {
                if (!string.IsNullOrWhiteSpace(cls))
                    _cssClasses.Remove(cls);
            }

            return this;
        }

        public string Generate(string content)
        {
            using QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);

            using SvgQRCode qrCode = new SvgQRCode(qrCodeData);
            string svgQrCode = qrCode.GetGraphic(_size,
                Color.Black, Color.White,
                false,
                QRCoder.SvgQRCode.SizingMode.ViewBoxAttribute);

            if (_cssClasses.Count > 0)
                svgQrCode = AddClassesToSvg(svgQrCode, _cssClasses);

            return svgQrCode;
        }

        private string AddClassesToSvg(string svgContent, IEnumerable<string> cssClasses)
        {
            string pattern = @"<svg([^>]*?)(class=""[^""]*"")?([^>]*?)>";

            if (Regex.IsMatch(svgContent, pattern))
            {
                svgContent = Regex.Replace(svgContent, pattern, match =>
                {
                    var classAttribute = match.Groups[2].Value;
                    string classesToAdd = string.Join(" ", cssClasses);

                    if (string.IsNullOrEmpty(classAttribute))
                    {
                        return $"<svg{match.Groups[1].Value} class=\"{classesToAdd}\"{match.Groups[3].Value}>";
                    }
                    else
                    {
                        var existingClass = classAttribute.Substring(7, classAttribute.Length - 8); // удалить class=" и "
                        var allClasses = existingClass.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                        // Добавим новые, которых ещё нет
                        foreach (var cls in cssClasses)
                        {
                            if (!allClasses.Contains(cls))
                                allClasses.Add(cls);
                        }

                        string newClassAttr = string.Join(" ", allClasses);
                        return $"<svg{match.Groups[1].Value} class=\"{newClassAttr}\"{match.Groups[3].Value}>";
                    }
                });
            }
            return svgContent;
        }
    }
}
