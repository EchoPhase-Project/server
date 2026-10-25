// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Files
{
    /// <summary>
    /// What a file is, from its first bytes rather than from what the
    /// uploader claims, and what each purpose takes. Text is never served as
    /// HTML or SVG: a user's file must not run as a page of the site.
    /// </summary>
    public static class FileContent
    {
        /// <summary>How many first bytes <see cref="Detect"/> looks at.</summary>
        public const int HeadLength = 4096;

        public const string Octets = "application/octet-stream";

        private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        private static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];
        private static ReadOnlySpan<byte> Gzip => [0x1F, 0x8B];

        private static readonly HashSet<string> Images = ["image/png", "image/jpeg", "image/webp", "image/gif"];
        private static readonly HashSet<string> AvatarTypes = ["image/png", "image/jpeg", "image/webp"];
        private static readonly HashSet<string> ImportTypes =
            ["application/zip", "application/gzip", "application/json", "text/csv", "text/plain"];

        /// <summary>The content type of a file starting with <paramref name="head"/>.</summary>
        /// <param name="head">Its first bytes (up to <see cref="HeadLength"/>).</param>
        /// <param name="name">Its name: only tells text formats apart (JSON, CSV).</param>
        public static string Detect(ReadOnlySpan<byte> head, string name)
        {
            if (head.StartsWith(Png))
                return "image/png";
            if (head.StartsWith(Jpeg))
                return "image/jpeg";
            if (head.StartsWith("GIF87a"u8) || head.StartsWith("GIF89a"u8))
                return "image/gif";
            if (head.Length >= 12 && head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8))
                return "image/webp";
            if (head.StartsWith("%PDF-"u8))
                return "application/pdf";
            if (head.StartsWith("PK\u0003\u0004"u8) || head.StartsWith("PK\u0005\u0006"u8))
                return "application/zip";
            if (head.StartsWith(Gzip))
                return "application/gzip";
            if (head.Length > 0 && IsText(head))
            {
                return Path.GetExtension(name).ToLowerInvariant() switch
                {
                    ".json" => "application/json",
                    ".csv" => "text/csv",
                    _ => "text/plain",
                };
            }
            return Octets;
        }

        /// <summary>Whether a file of this type may be uploaded for the purpose.</summary>
        public static bool Accepts(FilePurpose purpose, string contentType) => purpose switch
        {
            FilePurpose.Avatar => AvatarTypes.Contains(contentType),
            FilePurpose.Import => ImportTypes.Contains(contentType),
            _ => true,
        };

        /// <summary>Whether browsers may show it in place: raster images only.</summary>
        public static bool IsInline(string contentType) => Images.Contains(contentType);

        // UTF-8 without NUL; a character cut off at the end of the head is fine.
        private static bool IsText(ReadOnlySpan<byte> head)
        {
            if (head.Contains((byte)0))
                return false;
            var decoder = new UTF8Encoding(false, throwOnInvalidBytes: true).GetDecoder();
            try
            {
                Span<char> chars = stackalloc char[head.Length];
                decoder.GetChars(head, chars, flush: false);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }
    }
}
