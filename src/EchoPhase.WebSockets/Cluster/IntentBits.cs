// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Globalization;

namespace EchoPhase.WebSockets.Cluster
{
    /// <summary>A connection's intents as text and back: "length:base64".</summary>
    public static class IntentBits
    {
        public static string Write(BitArray bits)
        {
            var bytes = new byte[(bits.Length + 7) / 8];
            bits.CopyTo(bytes, 0);
            return $"{bits.Length.ToString(CultureInfo.InvariantCulture)}:{Convert.ToBase64String(bytes)}";
        }

        /// <summary>Null for text that is not one (a member written by another version).</summary>
        public static BitArray? Read(string text)
        {
            var colon = text.IndexOf(':');
            if (colon < 0 || !int.TryParse(text.AsSpan(0, colon), NumberStyles.None, CultureInfo.InvariantCulture, out var length))
                return null;
            try
            {
                var bytes = Convert.FromBase64String(text[(colon + 1)..]);
                if (length > bytes.Length * 8)
                    return null;
                return new BitArray(bytes) { Length = length };
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
