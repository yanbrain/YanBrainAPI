// File: Assets/Scripts/YanBrainAPI/Documents/DocumentReader.cs

using System;
using System.IO;
using System.Text;

namespace YanBrainAPI.Documents
{
    /// <summary>
    /// Reads a document from disk and returns plain text.
    /// (In your embedding flow this reads already-converted .txt/.md from ConvertedDocuments.)
    /// </summary>
    public sealed class DocumentReader
    {
        private readonly Encoding _encoding;

        public DocumentReader(Encoding encoding = null)
        {
            _encoding = encoding ?? Encoding.UTF8;
        }

        public string ReadAllText(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
                throw new ArgumentException("Path required", nameof(absolutePath));

            if (!File.Exists(absolutePath))
                throw new FileNotFoundException("File not found", absolutePath);

            var text = File.ReadAllText(absolutePath, _encoding);
            return text ?? string.Empty;
        }
    }
}
