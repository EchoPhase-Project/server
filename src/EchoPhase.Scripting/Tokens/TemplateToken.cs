// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tokens
{
    public class TemplateToken
    {
        public TemplateTokenType Type
        {
            get;
        }
        public string Value
        {
            get;
        }

        public TemplateToken(TemplateTokenType type, string value)
        {
            Type = type;
            Value = value;
        }
    }
}
