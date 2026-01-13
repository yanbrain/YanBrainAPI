using System;
using System.Collections.Generic;
using System.IO;

namespace YanBrainAPI.Utils
{
    public static class FileFilter
    {
        // Noise / junk files
        private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".meta",
            ".tmp",
            ".log"
        };

        private static readonly HashSet<string> IgnoredFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ".DS_Store"
        };

        // Server supported formats
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".doc", ".rtf",
            ".xlsx", ".xls",
            ".pptx", ".ppt",
            ".odt", ".odp", ".ods",
            ".txt", ".md"
        };

        public static bool IsValid(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(filePath);

            if (IgnoredFileNames.Contains(fileName))
                return false;

            if (IgnoredExtensions.Contains(extension))
                return false;

            // Only allow what the server supports
            return SupportedExtensions.Contains(extension);
        }
    }
}