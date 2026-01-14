// File: Assets/Scripts/YanBrainAPI/Conversion/ConversionService.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YanBrainAPI.Documents;
using YanBrainAPI.Networking;
using YanBrainAPI.Utils;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.Conversion
{
    /// <summary>
    /// Converts source documents into plain text via backend with async I/O.
    /// OPTIMIZATIONS:
    /// 1. Async file writes (prevents Unity freezing)
    /// 2. Progress throttling (prevents UI spam)
    /// 3. Cached file enumeration (faster scans)
    /// </summary>
    [EnableLogger]
    public sealed class ConversionService
    {
        private readonly YanBrainApi _api;
        private readonly YanBrainApiConfig _config;
        private readonly DocumentPathMapper _paths;

        private readonly object _stateLock = new object();
        private readonly ManualResetEventSlim _pauseGate = new ManualResetEventSlim(true);

        private CancellationTokenSource _runCts;

        private readonly DocumentProgressReporter _progress;

        public event Action<DocumentProgress> OnProgressChanged;

        public ConversionService(YanBrainApi api, YanBrainApiConfig config)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _paths = new DocumentPathMapper(config);

            _progress = new DocumentProgressReporter(DocumentWorkStage.Converting);
            _progress.OnChanged += p => OnProgressChanged?.Invoke(p);
        }

        // ==================== Progress / Control API ====================

        public DocumentProgress GetProgressSnapshot() => _progress.Snapshot();

        public bool IsRunning => _progress.Snapshot().IsRunning;
        public bool IsPaused => _progress.Snapshot().IsPaused;

        public void Pause()
        {
            lock (_stateLock)
            {
                if (!IsRunning) return;
            }
            _pauseGate.Reset();
            _progress.SetPaused(true);
            Log("[ConversionService] Paused");
        }

        public void Resume()
        {
            lock (_stateLock)
            {
                if (!IsRunning) return;
            }
            _pauseGate.Set();
            _progress.SetPaused(false);
            Log("[ConversionService] Resumed");
        }

        public void Cancel()
        {
            CancellationTokenSource ctsToCancel = null;

            lock (_stateLock)
            {
                if (!IsRunning) return;
                ctsToCancel = _runCts;
            }

            _pauseGate.Set();
            _progress.SetCancelling();

            try { ctsToCancel?.Cancel(); } catch { /* ignore */ }

            LogWarning("[ConversionService] Cancel requested");
        }

        public void ResetProgress()
        {
            _pauseGate.Set();
            _progress.Reset("Ready");
        }

        // ==================== Public API ====================

        public async Task ConvertAllAsync(CancellationToken externalCt = default)
        {
            lock (_stateLock)
            {
                if (IsRunning)
                    throw new InvalidOperationException("Conversion run already in progress.");
            }

            _pauseGate.Set();

            _runCts?.Dispose();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = _runCts.Token;

            try
            {
                _config.EnsureFoldersExist();

                var sourceRoot = _config.GetSourceDocumentsPath();
                if (!Directory.Exists(sourceRoot))
                {
                    _progress.Start(0, $"SourceDocuments not found: {sourceRoot}");
                    LogWarning($"[ConversionService] SourceDocuments not found: {sourceRoot}");
                    _progress.Stop();
                    return;
                }

                // ✅ OPTIMIZATION: Use cached file enumeration
                var files = DocumentFileEnumerator.EnumerateRelativeFiles(
                        sourceRoot, 
                        FileFilter.IsValid,
                        useCache: true
                    )
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _progress.Start(files.Count, files.Count == 0 ? "No supported files found" : "Running");

                if (files.Count == 0)
                {
                    LogWarning("[ConversionService] No supported files found in SourceDocuments (recursive)");
                    _progress.Stop();
                    return;
                }

                Log($"[ConversionService] Found {files.Count} supported source files (recursive)");

                const int batchSize = 8;

                for (int i = 0; i < files.Count; i += batchSize)
                {
                    ct.ThrowIfCancellationRequested();
                    await WaitIfPausedAsync(ct);

                    var batch = files.Skip(i).Take(batchSize).ToList();
                    var toConvert = batch.Where(rel => NeedsConversion(rel)).ToList();

                    // Up-to-date files count as OK
                    foreach (var rel in batch.Where(rel => !toConvert.Contains(rel)))
                    {
                        ct.ThrowIfCancellationRequested();
                        await WaitIfPausedAsync(ct);

                        _progress.SetCurrent(rel);
                        _progress.MarkOk();
                    }

                    if (toConvert.Count == 0)
                    {
                        Log($"[ConversionService] Batch {i / batchSize + 1}: all up to date, skipping");
                        continue;
                    }

                    _progress.SetCurrent($"Batch ({toConvert.Count} files)");
                    await ConvertBatchAsync(toConvert, ct);
                    
                    // ✅ OPTIMIZATION: Flush progress updates after each batch
                    _progress.FlushPending();
                }

                _progress.Complete();
                Log($"[ConversionService] ✅ Complete: {_progress.Snapshot().Done}/{_progress.Snapshot().Total} (OK: {_progress.Snapshot().Ok}, Failed: {_progress.Snapshot().Failed})");
            }
            catch (OperationCanceledException)
            {
                _progress.Cancelled();
                LogWarning("[ConversionService] Cancelled");
            }
            finally
            {
                _pauseGate.Set();

                try { _runCts?.Dispose(); } catch { /* ignore */ }
                _runCts = null;

                _progress.Stop();
            }
        }

        public async Task ConvertSingleAsync(string sourceRelativePath, CancellationToken externalCt = default)
        {
            if (string.IsNullOrWhiteSpace(sourceRelativePath))
                throw new ArgumentException("sourceRelativePath required", nameof(sourceRelativePath));

            var sourceRel = RelativePath.Normalize(sourceRelativePath);
            RelativePath.AssertSafe(sourceRel, nameof(sourceRelativePath));

            _config.EnsureFoldersExist();

            _runCts?.Dispose();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = _runCts.Token;

            await WaitIfPausedAsync(ct);

            var sourceAbs = _paths.SourceAbsolute(sourceRel);
            if (!FileFilter.IsValid(sourceAbs))
            {
                Log($"[ConversionService] Ignoring unsupported file: {sourceRel}");
                return;
            }

            if (!NeedsConversion(sourceRel))
            {
                Log($"[ConversionService] {sourceRel} up to date, skipping");
                return;
            }

            _progress.Start(1, "Running");
            _progress.SetCurrent(sourceRel);

            try
            {
                await ConvertBatchAsync(new List<string> { sourceRel }, ct);
                _progress.Complete();
            }
            catch (OperationCanceledException)
            {
                _progress.Cancelled();
                throw;
            }
            finally
            {
                _progress.Stop();
            }
        }

        public List<string> GetConvertedTextFiles()
        {
            var convertedRoot = _config.GetConvertedDocumentsPath();
            if (!Directory.Exists(convertedRoot)) return new List<string>();

            try
            {
                // ✅ OPTIMIZATION: Use cached file enumeration
                return DocumentFileEnumerator.EnumerateRelativeFiles(
                        convertedRoot,
                        abs => Path.GetExtension(abs).Equals(".txt", StringComparison.OrdinalIgnoreCase) 
                               && FileFilter.IsValid(abs),
                        useCache: true
                    )
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
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
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    File.Delete(file);

                foreach (var subDir in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories)
                             .OrderByDescending(d => d.Length))
                {
                    if (Directory.Exists(subDir) && !Directory.EnumerateFileSystemEntries(subDir).Any())
                        Directory.Delete(subDir, false);
                }

                // ✅ OPTIMIZATION: Invalidate cache after clearing
                DocumentFileEnumerator.InvalidateCache(dir);

                Log("[ConversionService] ConvertedDocuments cleared");
            }
            catch (Exception ex)
            {
                LogError($"[ConversionService] Failed to clear ConvertedDocuments: {ex.Message}");
            }
        }

        // ==================== Internals ====================

        private async Task ConvertBatchAsync(List<string> sourceRelativePaths, CancellationToken ct)
        {
            var uploads = new List<FileUpload>();
            var planned = new List<string>();

            foreach (var relAny in sourceRelativePaths)
            {
                ct.ThrowIfCancellationRequested();
                await WaitIfPausedAsync(ct);

                var sourceRel = RelativePath.Normalize(relAny);
                RelativePath.AssertSafe(sourceRel, nameof(sourceRelativePaths));

                var fullPath = _paths.SourceAbsolute(sourceRel);

                if (!FileFilter.IsValid(fullPath))
                    continue;

                if (!File.Exists(fullPath))
                {
                    MarkFail(sourceRel, $"Missing file: {fullPath}");
                    continue;
                }

                var bytes = File.ReadAllBytes(fullPath);
                var base64 = Convert.ToBase64String(bytes);

                uploads.Add(new FileUpload
                {
                    Filename = sourceRel,
                    ContentBase64 = base64
                });
                planned.Add(sourceRel);
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
                foreach (var rel in planned)
                    MarkFail(rel, "No converted text returned by API");
                return;
            }

            var returned = payload.Files
                .Where(f => !string.IsNullOrWhiteSpace(f.Filename))
                .ToDictionary(
                    f => RelativePath.Normalize(f.Filename),
                    f => f,
                    StringComparer.OrdinalIgnoreCase
                );

            foreach (var sourceRel in planned)
            {
                ct.ThrowIfCancellationRequested();
                await WaitIfPausedAsync(ct);

                _progress.SetCurrent(sourceRel);

                if (!returned.TryGetValue(sourceRel, out var converted))
                {
                    MarkFail(sourceRel, "Missing converted result from API");
                    continue;
                }

                try
                {
                    var convertedRelTxt = _paths.ToConvertedRelativeTxt(sourceRel);
                    var outAbs = _paths.ConvertedAbsolute(convertedRelTxt);

                    // ✅ CRITICAL FIX: Async file write (off main thread)
                    await Task.Run(() =>
                    {
                        RelativePath.EnsureParentDirectory(outAbs);
                        File.WriteAllText(outAbs, converted.Text ?? string.Empty);
                    }, ct);

                    Log($"[ConversionService] ✓ Saved {convertedRelTxt} ({converted.CharacterCount} chars)");
                    _progress.MarkOk();
                }
                catch (Exception ex)
                {
                    MarkFail(sourceRel, ex.Message);
                }
            }

            Log($"[ConversionService] Batch done. Credits charged: {payload.TotalCreditsCharged}");
        }

        private void MarkFail(string rel, string reason)
        {
            LogError($"[ConversionService] Failed {rel}: {reason}");
            _progress.SetCurrent(rel);
            _progress.MarkFailed();
        }

        private bool NeedsConversion(string sourceRelativePath)
        {
            var sourceRel = RelativePath.Normalize(sourceRelativePath);
            RelativePath.AssertSafe(sourceRel, nameof(sourceRelativePath));

            var sourceAbs = _paths.SourceAbsolute(sourceRel);
            if (!File.Exists(sourceAbs)) return false;
            if (!FileFilter.IsValid(sourceAbs)) return false;

            var convertedRelTxt = _paths.ToConvertedRelativeTxt(sourceRel);
            var outAbs = _paths.ConvertedAbsolute(convertedRelTxt);

            if (!File.Exists(outAbs))
                return true;

            try
            {
                var sourceTime = File.GetLastWriteTimeUtc(sourceAbs);
                var outTime = File.GetLastWriteTimeUtc(outAbs);
                return sourceTime > outTime;
            }
            catch
            {
                return true;
            }
        }

        private async Task WaitIfPausedAsync(CancellationToken ct)
        {
            if (_pauseGate.IsSet) return;

            while (!_pauseGate.IsSet)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(50, ct);
            }
        }
    }
}