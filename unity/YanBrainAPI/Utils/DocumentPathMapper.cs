using System;
using System.IO;

namespace YanBrainAPI.Utils
{
    /// <summary>
    /// Single source of truth for mapping across pipeline stages while preserving structure.
    /// </summary>
    public sealed class DocumentPathMapper
    {
        private readonly YanBrainApiConfig _config;
        private readonly string _sourceRootAbs;
        private readonly string _convertedRootAbs;
        private readonly string _embeddingsRootAbs;

        public DocumentPathMapper(YanBrainApiConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _sourceRootAbs = _config.GetSourceDocumentsPath();
            _convertedRootAbs = _config.GetConvertedDocumentsPath();
            _embeddingsRootAbs = _config.GetEmbeddingsPath();
        }

        public string SourceAbsolute(string sourceRelative)
        {
            var rel = RelativePath.Normalize(sourceRelative);
            RelativePath.AssertSafe(rel, nameof(sourceRelative));
            return RelativePath.CombineAbsolute(_sourceRootAbs, rel);
        }

        public string ConvertedAbsolute(string convertedRelativeTxt)
        {
            var rel = RelativePath.Normalize(convertedRelativeTxt);
            RelativePath.AssertSafe(rel, nameof(convertedRelativeTxt));
            return RelativePath.CombineAbsolute(_convertedRootAbs, rel);
        }

        public string EmbeddingsAbsolute(string convertedRelativeTxt)
        {
            var rel = RelativePath.Normalize(convertedRelativeTxt);
            RelativePath.AssertSafe(rel, nameof(convertedRelativeTxt));
            return RelativePath.CombineAbsolute(_embeddingsRootAbs, rel + ".embeddings");
        }

        /// <summary>
        /// Convert a source relative path to a converted .txt relative path, keeping directories.
        /// "folder1/file1.pdf" -> "folder1/file1.txt"
        /// </summary>
        public string ToConvertedRelativeTxt(string sourceRelative)
        {
            var rel = RelativePath.Normalize(sourceRelative);
            RelativePath.AssertSafe(rel, nameof(sourceRelative));
            return RelativePath.ChangeExtensionKeepDirs(rel, ".txt");
        }
    }
}
