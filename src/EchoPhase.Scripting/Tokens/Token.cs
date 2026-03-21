// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;

namespace EchoPhase.Scripting.Tokens
{
    public class Token
    {
        public TokenType Type;
        private readonly string _source;
        private readonly Range _textRange;

        private double? _numberCache;

        public ReadOnlySpan<char> Span => _source.AsSpan(_textRange.Start.Value, _textRange.End.Value - _textRange.Start.Value);

        public string Text => Span.ToString();

        public double Number
        {
            get
            {
                if (_numberCache.HasValue) return _numberCache.Value;

                if (Type == TokenType.Number)
                    _numberCache = double.Parse(Text, CultureInfo.InvariantCulture);
                else
                    _numberCache = 0;

                return _numberCache.Value;
            }
        }

        public Token(TokenType type, string source, int start, int end)
        {
            Type = type;
            _source = source;
            _textRange = start..end;
        }
    }
}
