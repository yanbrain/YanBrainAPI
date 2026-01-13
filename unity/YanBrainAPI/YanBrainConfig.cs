using System.IO;
using UnityEngine;

namespace YanBrainAPI
{
    [CreateAssetMenu(menuName = "YanBrainAPI/YanBrainConfig", fileName = "YanBrainConfig")]
    public class YanBrainConfig : ScriptableObject
    {
        [Header("Backend")]
        public string BaseUrl = "http://localhost:8080";
        [Range(5, 120)] public int TimeoutSeconds = 60;

        [Header("Local Files Root (relative)")]
        public string RootFolderName = "Files";

        [Header("Subfolder Names (editable)")]
        public string SourceDocumentsFolder = "SourceDocuments";
        public string ConvertedDocumentsFolder = "ConvertedDocuments";
        public string EmbeddingsFolder = "Embeddings";
        public string IndexFolder = "Index";
        public string ImagesFolder = "Images";
        public string AudioFolder = "Audio";

        public string GetBaseUrl() =>
            string.IsNullOrWhiteSpace(BaseUrl) ? "" : BaseUrl.Trim().TrimEnd('/');

        /// <summary>
        /// Gets the absolute path to the Files root folder.
        /// Editor: Assets/Files/...
        /// Standalone (Windows): <exe-folder>/Files/...
        /// Standalone (macOS): <MyGame.app>/Files/...
        /// </summary>
        public string GetFilesRootAbsolute()
        {
            var dataPath = Application.dataPath;

            string rootBase;
            if (Application.isEditor)
            {
                // Editor: .../Project/Assets
                rootBase = dataPath;
            }
            else
            {
                var dir = new DirectoryInfo(dataPath);

                // Windows/Linux: .../MyGame_Data -> parent is exe folder
                if (dir.Name.EndsWith("_Data"))
                {
                    rootBase = dir.Parent?.FullName ?? dataPath;
                }
                // macOS: .../MyGame.app/Contents -> use .app folder
                else if (dir.FullName.Contains(".app"))
                {
                    // Find the .app directory
                    var current = dir;
                    while (current != null && !current.Name.EndsWith(".app"))
                    {
                        current = current.Parent;
                    }
                    rootBase = current?.FullName ?? dir.Parent?.FullName ?? dataPath;
                }
                // Fallback: use parent of dataPath
                else
                {
                    rootBase = dir.Parent?.FullName ?? dataPath;
                }
            }

            return Path.Combine(rootBase, RootFolderName);
        }

        public string GetSourceDocumentsPath() =>
            Path.Combine(GetFilesRootAbsolute(), SourceDocumentsFolder);

        public string GetConvertedDocumentsPath() =>
            Path.Combine(GetFilesRootAbsolute(), ConvertedDocumentsFolder);

        public string GetEmbeddingsPath() =>
            Path.Combine(GetFilesRootAbsolute(), EmbeddingsFolder);

        public string GetIndexPath() =>
            Path.Combine(GetFilesRootAbsolute(), IndexFolder);

        public string GetImagesPath() =>
            Path.Combine(GetFilesRootAbsolute(), ImagesFolder);

        public string GetAudioPath() =>
            Path.Combine(GetFilesRootAbsolute(), AudioFolder);

        /// <summary>
        /// Creates all required directories if they don't exist.
        /// Safe to call multiple times.
        /// </summary>
        public void EnsureFoldersExist()
        {
            CreateDirectorySafe(GetFilesRootAbsolute());
            CreateDirectorySafe(GetSourceDocumentsPath());
            CreateDirectorySafe(GetConvertedDocumentsPath());
            CreateDirectorySafe(GetEmbeddingsPath());
            CreateDirectorySafe(GetIndexPath());
            CreateDirectorySafe(GetImagesPath());
            CreateDirectorySafe(GetAudioPath());
        }

        private static void CreateDirectorySafe(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to create directory {path}: {ex.Message}");
            }
        }

        // ✅ Added: Validation in OnValidate
        private void OnValidate()
        {
            // Ensure timeout is reasonable
            TimeoutSeconds = Mathf.Clamp(TimeoutSeconds, 5, 120);
            
            // Ensure folder names are valid
            RootFolderName = SanitizeFolderName(RootFolderName, "Files");
            SourceDocumentsFolder = SanitizeFolderName(SourceDocumentsFolder, "SourceDocuments");
            ConvertedDocumentsFolder = SanitizeFolderName(ConvertedDocumentsFolder, "ConvertedDocuments");
            EmbeddingsFolder = SanitizeFolderName(EmbeddingsFolder, "Embeddings");
            ImagesFolder = SanitizeFolderName(ImagesFolder, "Images");
            AudioFolder = SanitizeFolderName(AudioFolder, "Audio");
        }

        private static string SanitizeFolderName(string name, string defaultName)
        {
            if (string.IsNullOrWhiteSpace(name))
                return defaultName;

            // Remove invalid path characters
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                name = name.Replace(c, '_');
            }

            return name.Trim();
        }
    }
}