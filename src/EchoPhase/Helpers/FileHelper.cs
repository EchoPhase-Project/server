// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Helpers
{
    /// <summary>
    /// Provides utility methods for handling file-related operations, such as determining content types (MIME types).
    /// </summary>
    public class FileHelper
    {
        /// <summary>
        /// Gets the content type (MIME type) for a given file path based on its extension.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>
        /// The corresponding content type (MIME type) if known; otherwise, <c>"application/octet-stream"</c>.
        /// </returns>
        public string GetContentType(string path)
        {
            var types = GetMimeTypes();
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return types.ContainsKey(ext) ? types[ext] : "application/octet-stream";
        }

        /// <summary>
        /// Returns a dictionary of commonly used file extensions and their corresponding MIME types.
        /// </summary>
        /// <returns>
        /// A dictionary where the key is a file extension (e.g., ".txt") and the value is the MIME type (e.g., "text/plain").
        /// </returns>
        public Dictionary<string, string> GetMimeTypes()
        {
            return new Dictionary<string, string>
            {
                {".txt", "text/plain"},
                {".pdf", "application/pdf"},
                {".doc", "application/vnd.ms-word"},
                {".docx", "application/vnd.ms-word"},
                {".xls", "application/vnd.ms-excel"},
                {".xlsx", "application/vnd.openxmlformats.officedocument.spreadsheetml.sheet"},
                {".png", "image/png"},
                {".jpg", "image/jpeg"},
                {".jpeg", "image/jpeg"},
                {".gif", "image/gif"},
                {".csv", "text/csv"}
            };
        }
    }
}
