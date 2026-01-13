using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YanBrainAPI.Networking;
using YanBrainAPI.Utils; // ✅ uses FileFilter
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.Conversion
{
    /// <summary>
    /// Converts source documents (PDF/DOCX/etc) into plain text via backend,
    /// then saves results into ConvertedDocuments folder as .txt files.
    /// </summary>
    [EnableLogger]
    public sealed class ConversionService
    {
        private readonly YanBrainApi _api;
        private readonly YanBrainApiConfig _config;

        public ConversionService(YanBrainApi api, YanBrainApiConfig config)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        // ==================== Public API ====================

        public async Task ConvertAllAsync(CancellationToken ct = default)
        {
            _config.EnsureFoldersExist();

            var sourceDir = _config.GetSourceDocumentsPath();
            if (!Directory.Exists(sourceDir))
            {
                LogWarning($"[ConversionService] SourceDocuments not found: {sourceDir}");
                return;
            }

            // ✅ Filter: ignore Unity/system junk + allow only server-supported extensions
            var files = Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly)
                .Where(FileFilter.IsValid)
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .OrderBy(f => f)
                .ToList();

            if (files.Count == 0)
            {
                LogWarning("[ConversionService] No supported files found in SourceDocuments");
                return;
            }

            Log($"[ConversionService] Found {files.Count} supported source files");

            // Convert in batches to avoid huge payloads
            const int batchSize = 8;
            for (int i = 0; i < files.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batch = files.Skip(i).Take(batchSize).ToList();

                // Skip batch items that are up-to-date
                var toConvert = batch
                    .Where(filename => NeedsConversion(filename))
                    .ToList();

                if (toConvert.Count == 0)
                {
                    Log($"[ConversionService] Batch {i / batchSize + 1}: all up to date, skipping");
                    continue;
                }

                await ConvertBatchAsync(toConvert, ct);
            }

            Log("[ConversionService] ✅ All conversions complete");
        }

        public async Task ConvertSingleAsync(string sourceFilename, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sourceFilename))
                throw new ArgumentException("sourceFilename required", nameof(sourceFilename));

            _config.EnsureFoldersExist();

            var sourcePath = Path.Combine(_config.GetSourceDocumentsPath(), sourceFilename);

            // ✅ Safety: ignore junk / unsupported types even if passed explicitly
            if (!FileFilter.IsValid(sourcePath))
            {
                Log($"[ConversionService] Ignoring unsupported file: {sourceFilename}");
                return;
            }

            if (!NeedsConversion(sourceFilename))
            {
                Log($"[ConversionService] {sourceFilename} up to date, skipping");
                return;
            }

            await ConvertBatchAsync(new List<string> { sourceFilename }, ct);
        }

        public List<string> GetConvertedTextFiles()
        {
            var dir = _config.GetConvertedDocumentsPath();
            if (!Directory.Exists(dir)) return new List<string>();

            try
            {
                return Directory.GetFiles(dir, "*.txt")
                    .Select(Path.GetFileName)
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .OrderBy(f => f)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public void ClearConverted()
        {
            var dir = _config.GetConvertedDocumentsPath();
            if (!Directory.Exists(dir)) return;

            try
            {
                foreach (var file in Directory.GetFiles(dir))
                    File.Delete(file);

                Log("[ConversionService] ConvertedDocuments cleared");
            }
            catch (Exception ex)
            {
                LogError($"[ConversionService] Failed to clear ConvertedDocuments: {ex.Message}");
            }
        }

        // ==================== Internals ====================

        private async Task ConvertBatchAsync(List<string> sourceFilenames, CancellationToken ct)
        {
            var sourceDir = _config.GetSourceDocumentsPath();
            var uploads = new List<FileUpload>();

            foreach (var filename in sourceFilenames)
            {
                ct.ThrowIfCancellationRequested();

                var fullPath = Path.Combine(sourceDir, filename);

                // ✅ Double safety
                if (!FileFilter.IsValid(fullPath))
                    continue;

                if (!File.Exists(fullPath))
                {
                    LogWarning($"[ConversionService] Source file missing: {fullPath}");
                    continue;
                }

                byte[] bytes = File.ReadAllBytes(fullPath);
                var base64 = Convert.ToBase64String(bytes);

                uploads.Add(new FileUpload
                {
                    Filename = filename,
                    ContentBase64 = base64
                });
            }

            if (uploads.Count == 0)
            {
                LogWarning("[ConversionService] Nothing to upload in this batch");
                return;
            }

            Log($"[ConversionService] Converting batch: {uploads.Count} files...");

            var payload = await _api.DocumentConvertAsync(uploads, ct);

            if (payload?.Files == null || payload.Files.Count == 0)
            {
                LogWarning("[ConversionService] No converted text returned by API");
                return;
            }

            var outDir = _config.GetConvertedDocumentsPath();

            foreach (var converted in payload.Files)
            {
                ct.ThrowIfCancellationRequested();

                var outName = GetConvertedTxtName(converted.Filename);
                var outPath = Path.Combine(outDir, outName);

                try
                {
                    File.WriteAllText(outPath, converted.Text ?? string.Empty);
                    Log($"[ConversionService] ✓ Saved {outName} ({converted.CharacterCount} chars)");
                }
                catch (Exception ex)
                {
                    LogError($"[ConversionService] Failed to write {outName}: {ex.Message}");
                }
            }

            Log($"[ConversionService] Batch done. Credits charged: {payload.TotalCreditsCharged}");
        }

        private bool NeedsConversion(string sourceFilename)
        {
            var sourcePath = Path.Combine(_config.GetSourceDocumentsPath(), sourceFilename);
            if (!File.Exists(sourcePath)) return false;

            // ✅ If it’s junk/unsupported, treat as “no”
            if (!FileFilter.IsValid(sourcePath))
                return false;

            var outName = GetConvertedTxtName(sourceFilename);
            var outPath = Path.Combine(_config.GetConvertedDocumentsPath(), outName);

            if (!File.Exists(outPath))
                return true;

            try
            {
                var sourceTime = File.GetLastWriteTimeUtc(sourcePath);
                var outTime = File.GetLastWriteTimeUtc(outPath);
                return sourceTime > outTime;
            }
            catch
            {
                return true;
            }
        }

        private static string GetConvertedTxtName(string sourceFilename)
        {
            // Always save as .txt
            var safeBase = Path.GetFileNameWithoutExtension(sourceFilename);
            if (string.IsNullOrWhiteSpace(safeBase))
                safeBase = "document";

            return safeBase + ".txt";
        }
    }
}
