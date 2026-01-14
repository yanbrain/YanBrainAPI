using System;
using System.IO;
using System.Linq;

namespace YanBrainAPI.Utils
{
    /// <summary>
    /// Canonical relative path policy for the whole pipeline.
    /// Stored form:
    /// - relative (no drive, no leading slash)
    /// - forward slashes only
    /// - no ".."
    /// </summary>
    public static class RelativePath
    {
        public static string Normalize(string any)
        {
            if (string.IsNullOrWhiteSpace(any))
                return string.Empty;

            // Convert backslashes to forward slashes, trim whitespace
            var s = any.Trim().Replace('\\', '/');

            // Remove leading "./"
            while (s.StartsWith("./", StringComparison.Ordinal))
                s = s.Substring(2);

            // Collapse repeated slashes
            while (s.Contains("//", StringComparison.Ordinal))
                s = s.Replace("//", "/");

            // Trim leading slash (we only store relative paths)
            while (s.StartsWith("/", StringComparison.Ordinal))
                s = s.Substring(1);

            // Trim trailing slash
            while (s.EndsWith("/", StringComparison.Ordinal))
                s = s.Substring(0, s.Length - 1);

            return s;
        }

        public static bool IsSafe(string rel)
        {
            rel = Normalize(rel);
            if (string.IsNullOrEmpty(rel))
                return false;

            // Must be relative
            if (Path.IsPathRooted(rel))
                return false;

            // No parent traversal
            var parts = rel.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Any(p => p == ".."))
                return false;

            // No empty segments
            if (parts.Any(string.IsNullOrWhiteSpace))
                return false;

            return true;
        }

        public static void AssertSafe(string rel, string paramName)
        {
            if (!IsSafe(rel))
                throw new ArgumentException($"Unsafe relative path: \"{rel}\"", paramName);
        }

        public static string ChangeExtensionKeepDirs(string rel, string newExtWithDot)
        {
            rel = Normalize(rel);
            AssertSafe(rel, nameof(rel));

            if (string.IsNullOrWhiteSpace(newExtWithDot) || !newExtWithDot.StartsWith(".", StringComparison.Ordinal))
                throw new ArgumentException("newExtWithDot must start with '.'", nameof(newExtWithDot));

            var dir = GetDirectory(rel); // can be empty
            var baseName = Path.GetFileNameWithoutExtension(rel);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "document";

            var file = baseName + newExtWithDot;
            return string.IsNullOrEmpty(dir) ? file : $"{dir}/{file}";
        }

        public static string GetDirectory(string rel)
        {
            rel = Normalize(rel);
            AssertSafe(rel, nameof(rel));

            var idx = rel.LastIndexOf('/');
            return idx <= 0 ? string.Empty : rel.Substring(0, idx);
        }

        /// <summary>
        /// Combine an absolute root with a RELATIVE canonical path.
        /// </summary>
        public static string CombineAbsolute(string rootAbsolute, string rel)
        {
            if (string.IsNullOrWhiteSpace(rootAbsolute))
                throw new ArgumentException("rootAbsolute required", nameof(rootAbsolute));

            rel = Normalize(rel);
            AssertSafe(rel, nameof(rel));

            // Convert forward-slash rel to system path segments
            var segments = rel.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var path = rootAbsolute;
            foreach (var seg in segments)
                path = Path.Combine(path, seg);

            return path;
        }

        public static void EnsureParentDirectory(string absoluteFilePath)
        {
            if (string.IsNullOrWhiteSpace(absoluteFilePath))
                return;

            var dir = Path.GetDirectoryName(absoluteFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// Get relative path from rootAbsolute to absoluteFilePath, then normalize to forward slashes.
        /// </summary>
        public static string GetRelativePath(string rootAbsolute, string absoluteFilePath)
        {
            var rel = Path.GetRelativePath(rootAbsolute, absoluteFilePath);
            return Normalize(rel);
        }
    }
}
