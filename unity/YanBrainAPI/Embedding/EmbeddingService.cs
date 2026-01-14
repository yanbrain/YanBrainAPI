// File: Assets/Scripts/YanBrainAPI/Embedding/EmbeddingService.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YanBrainAPI.Documents;
using YanBrainAPI.Networking;
using YanBrainAPI.RAG;
using YanBrainAPI.Utils;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.Embedding
{
    /// <summary>
    /// Generates embeddings from converted documents with optimized batching and async I/O.
    /// OPTIMIZATIONS:
    /// 1. Async file writes (prevents Unity freezing)
    /// 2. Intelligent batching (maximizes API efficiency)
    /// 3. Progress throttling (prevents UI spam)
    /// 4. File-order preservation (safe cancellation)
    /// </summary>
    [EnableLogger]
    public sealed class EmbeddingService
    {
        private readonly RAGContext _context;
        private readonly DocumentReader _reader;
        private readonly DocumentChunker _chunker;
        private readonly DocumentPathMapper _paths;

        private readonly object _stateLock = new object();
        private readonly ManualResetEventSlim _pauseGate = new ManualResetEventSlim(true);

        private CancellationTokenSource _runCts;

        private readonly DocumentProgressReporter _progress;

        public event Action<DocumentProgress> OnProgressChanged;

        // ✅ OPTIMIZATION: Safer batch limits to prevent timeouts
        private const int MAX_CHARS_PER_BATCH = 500_000; // 500K chars
        private const int MAX_ITEMS_PER_BATCH = 1000; // 1K items

        public EmbeddingService(RAGContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _reader = new DocumentReader();
            _chunker = new DocumentChunker();
            _paths = new DocumentPathMapper(_context.ApiConfig);

            _progress = new DocumentProgressReporter(DocumentWorkStage.Embedding);
            _progress.OnChanged += p => OnProgressChanged?.Invoke(p);
        }

        // ==================== Public State API ====================

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
            Log("[EmbeddingService] Paused");
        }

        public void Resume()
        {
            lock (_stateLock)
            {
                if (!IsRunning) return;
            }
            _pauseGate.Set();
            _progress.SetPaused(false);
            Log("[EmbeddingService] Resumed");
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

            LogWarning("[EmbeddingService] Cancel requested");
        }

        public void ResetProgress()
        {
            _pauseGate.Set();
            _progress.Reset("Ready");
        }

        // ==================== Embedding API ====================

        /// <summary>
        /// Generate embeddings for a single converted document with async file I/O
        /// </summary>
        public async Task GenerateEmbeddingsAsync(string convertedRelativeTxtPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(convertedRelativeTxtPath))
                throw new ArgumentException("Filename required", nameof(convertedRelativeTxtPath));

            var convertedRel = RelativePath.Normalize(convertedRelativeTxtPath);
            RelativePath.AssertSafe(convertedRel, nameof(convertedRelativeTxtPath));

            var fileAbs = _paths.ConvertedAbsolute(convertedRel);

            if (!FileFilter.IsValid(fileAbs))
            {
                Log($"[EmbeddingService] Ignoring unsupported file: {convertedRel}");
                return;
            }

            if (!_context.Storage.NeedsReindex(convertedRel))
            {
                Log($"[EmbeddingService] {convertedRel} up to date, skipping");
                return;
            }

            if (!File.Exists(fileAbs))
                throw new FileNotFoundException($"Document not found: {fileAbs}");

            await WaitIfPausedAsync(ct);

            Log($"[EmbeddingService] Generating embeddings for {convertedRel}...");

            var text = _reader.ReadAllText(fileAbs);
            if (string.IsNullOrWhiteSpace(text))
                throw new Exception($"Document empty: {convertedRel}");

            await WaitIfPausedAsync(ct);

            var chunks = _chunker.Chunk(
                text,
                _context.RagConfig.ChunkSizeTokens,
                _context.RagConfig.OverlapTokens
            );

            if (chunks.Count == 0)
                throw new Exception($"No chunks produced: {convertedRel}");

            Log($"[EmbeddingService] {convertedRel}: {chunks.Count} chunks");

            await WaitIfPausedAsync(ct);

            var items = chunks.Select((chunk, i) => new EmbeddingItem
            {
                Id = $"{convertedRel}::chunk_{i}",
                Filename = convertedRel,
                Text = chunk
            }).ToList();

            await WaitIfPausedAsync(ct);

            var embedded = await _context.Api.EmbeddingsAsync(items, ct);

            if (embedded.Items == null || embedded.Items.Count != chunks.Count)
                throw new Exception($"Embedding count mismatch: {embedded?.Items?.Count ?? 0} vs {chunks.Count}");

            var docEmbeddings = new DocumentEmbeddings
            {
                Filename = convertedRel,
                ChunkSizeTokens = _context.RagConfig.ChunkSizeTokens,
                OverlapTokens = _context.RagConfig.OverlapTokens,
                Chunks = embedded.Items.Select((item, i) => new RagChunk
                {
                    ChunkIndex = i,
                    Text = chunks[i],
                    Embedding = item.Embedding
                }).ToList()
            };

            // ✅ CRITICAL FIX: Async file write (prevents Unity freezing)
            await _context.Storage.SaveDocumentEmbeddingsAsync(docEmbeddings);

            Log($"[EmbeddingService] ✓ {convertedRel} embedded");
        }

        /// <summary>
        /// Generate embeddings for all converted documents with optimized batching.
        /// OPTIMIZED: Intelligent batching + async I/O + progress throttling
        /// </summary>
        public async Task GenerateAllEmbeddingsAsync(CancellationToken externalCt = default)
        {
            _pauseGate.Set();

            _runCts?.Dispose();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = _runCts.Token;

            try
            {
                var convertedRoot = _context.ApiConfig.GetConvertedDocumentsPath();
                if (!Directory.Exists(convertedRoot))
                {
                    _progress.Start(0, $"ConvertedDocuments not found: {convertedRoot}");
                    LogWarning($"[EmbeddingService] ConvertedDocuments not found: {convertedRoot}");
                    _progress.Stop();
                    return;
                }

                // ✅ OPTIMIZATION: Use cached file enumeration
                var files = DocumentFileEnumerator.EnumerateRelativeFiles(
                        convertedRoot,
                        abs =>
                        {
                            var ext = Path.GetExtension(abs);
                            return (ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                                    ext.Equals(".md", StringComparison.OrdinalIgnoreCase))
                                   && FileFilter.IsValid(abs);
                        },
                        useCache: true
                    )
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _progress.Start(files.Count, files.Count == 0 ? "No documents to embed" : "Running");

                if (files.Count == 0)
                {
                    LogWarning("[EmbeddingService] No documents to embed");
                    _progress.Stop();
                    return;
                }

                Log($"[EmbeddingService] Generating embeddings for {files.Count} documents...");

                // ===== PHASE 1: Prepare all documents and chunks IN ORDER =====
                var allDocuments = new List<DocumentWithChunks>();

                foreach (var convertedRel in files)
                {
                    ct.ThrowIfCancellationRequested();
                    await WaitIfPausedAsync(ct);

                    try
                    {
                        var fileAbs = _paths.ConvertedAbsolute(convertedRel);

                        if (!FileFilter.IsValid(fileAbs))
                        {
                            Log($"[EmbeddingService] Ignoring unsupported file: {convertedRel}");
                            continue;
                        }

                        if (!_context.Storage.NeedsReindex(convertedRel))
                        {
                            Log($"[EmbeddingService] {convertedRel} up to date, skipping");
                            continue;
                        }

                        if (!File.Exists(fileAbs))
                        {
                            LogWarning($"[EmbeddingService] File not found: {convertedRel}");
                            continue;
                        }

                        var text = _reader.ReadAllText(fileAbs);
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            LogWarning($"[EmbeddingService] Document empty: {convertedRel}");
                            continue;
                        }

                        var chunks = _chunker.Chunk(
                            text,
                            _context.RagConfig.ChunkSizeTokens,
                            _context.RagConfig.OverlapTokens
                        );

                        if (chunks.Count == 0)
                        {
                            LogWarning($"[EmbeddingService] No chunks produced: {convertedRel}");
                            continue;
                        }

                        Log($"[EmbeddingService] {convertedRel}: {chunks.Count} chunks");

                        allDocuments.Add(new DocumentWithChunks
                        {
                            DocumentPath = convertedRel,
                            Chunks = chunks
                        });
                    }
                    catch (Exception ex)
                    {
                        LogError($"[EmbeddingService] Failed to prepare {convertedRel}: {ex.Message}");
                    }
                }

                if (allDocuments.Count == 0)
                {
                    LogWarning("[EmbeddingService] No documents to process after filtering");
                    _progress.Stop();
                    return;
                }

                // ===== PHASE 2: Create file-ordered batches =====
                var batches = CreateFileOrderedBatches(allDocuments);
                Log($"[EmbeddingService] Created {batches.Count} file-ordered batches");

                // ===== PHASE 3: Process batches with async I/O =====
                int processedBatches = 0;
                
                foreach (var batch in batches)
                {
                    ct.ThrowIfCancellationRequested();
                    await WaitIfPausedAsync(ct);

                    try
                    {
                        Log($"[EmbeddingService] Processing batch {++processedBatches}/{batches.Count}: {batch.Documents.Count} files, {batch.TotalChunks} chunks, {batch.TotalChars} chars");

                        // Flatten all chunks in this batch
                        var batchItems = new List<EmbeddingItem>();
                        var itemToDocMap = new List<(string docPath, int chunkIndex)>();

                        foreach (var doc in batch.Documents)
                        {
                            for (int i = 0; i < doc.Chunks.Count; i++)
                            {
                                batchItems.Add(new EmbeddingItem
                                {
                                    Id = $"{doc.DocumentPath}::chunk_{i}",
                                    Filename = doc.DocumentPath,
                                    Text = doc.Chunks[i]
                                });
                                itemToDocMap.Add((doc.DocumentPath, i));
                            }
                        }

                        // Single API call for entire batch
                        var embedded = await _context.Api.EmbeddingsAsync(batchItems, ct);

                        if (embedded.Items == null || embedded.Items.Count != batchItems.Count)
                        {
                            throw new Exception($"Embedding count mismatch: {embedded?.Items?.Count ?? 0} vs {batchItems.Count}");
                        }

                        // Map results back to documents
                        var docResults = new Dictionary<string, List<(int chunkIndex, string text, float[] embedding)>>();

                        for (int i = 0; i < embedded.Items.Count; i++)
                        {
                            var (docPath, chunkIndex) = itemToDocMap[i];

                            if (!docResults.ContainsKey(docPath))
                            {
                                docResults[docPath] = new List<(int, string, float[])>();
                            }

                            docResults[docPath].Add((
                                chunkIndex,
                                batchItems[i].Text,
                                embedded.Items[i].Embedding
                            ));
                        }

                        // ✅ CRITICAL FIX: Save documents with async I/O (prevents freezing)
                        foreach (var doc in batch.Documents)
                        {
                            ct.ThrowIfCancellationRequested();
                            await WaitIfPausedAsync(ct);

                            _progress.SetCurrent(doc.DocumentPath);

                            try
                            {
                                if (!docResults.ContainsKey(doc.DocumentPath))
                                {
                                    throw new Exception($"No embeddings returned for {doc.DocumentPath}");
                                }

                                var chunks = docResults[doc.DocumentPath].OrderBy(c => c.chunkIndex).ToList();

                                var docEmbeddings = new DocumentEmbeddings
                                {
                                    Filename = doc.DocumentPath,
                                    ChunkSizeTokens = _context.RagConfig.ChunkSizeTokens,
                                    OverlapTokens = _context.RagConfig.OverlapTokens,
                                    Chunks = chunks.Select(c => new RagChunk
                                    {
                                        ChunkIndex = c.chunkIndex,
                                        Text = c.text,
                                        Embedding = c.embedding
                                    }).ToList()
                                };

                                // ✅ CRITICAL FIX: Async save (off main thread)
                                await _context.Storage.SaveDocumentEmbeddingsAsync(docEmbeddings);
                                _progress.MarkOk();

                                Log($"[EmbeddingService] ✓ {doc.DocumentPath} saved ({chunks.Count} chunks)");
                            }
                            catch (Exception ex)
                            {
                                _progress.MarkFailed();
                                LogError($"[EmbeddingService] Failed to save {doc.DocumentPath}: {ex.Message}");
                            }
                        }

                        // ✅ OPTIMIZATION: Flush progress updates after each batch
                        _progress.FlushPending();

                        Log($"[EmbeddingService] ✓ Batch {processedBatches}/{batches.Count} complete");
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        LogError($"[EmbeddingService] Batch failed: {ex.Message}");
                        foreach (var doc in batch.Documents)
                        {
                            _progress.MarkFailed();
                        }
                        throw;
                    }
                }

                _progress.Complete();
                Log($"[EmbeddingService] ✅ Complete: {_progress.Snapshot().Done}/{_progress.Snapshot().Total} (OK: {_progress.Snapshot().Ok}, Failed: {_progress.Snapshot().Failed})");
            }
            catch (OperationCanceledException)
            {
                _progress.Cancelled();
                LogWarning("[EmbeddingService] Cancelled");
            }
            finally
            {
                _pauseGate.Set();

                try { _runCts?.Dispose(); }
                catch { /* ignore */ }
                finally { _runCts = null; }

                _progress.Stop();
            }
        }

        public List<string> GetEmbeddedDocuments()
        {
            return _context.Storage.GetEmbeddedDocuments();
        }

        public void RemoveEmbeddings(string convertedRelativeTxtPath)
        {
            var rel = RelativePath.Normalize(convertedRelativeTxtPath);
            RelativePath.AssertSafe(rel, nameof(convertedRelativeTxtPath));

            _context.Storage.RemoveDocument(rel);
            Log($"[EmbeddingService] Removed embeddings for {rel}");
        }

        public void ClearAllEmbeddings()
        {
            _context.Storage.ClearAll();
            Log("[EmbeddingService] All embeddings cleared");
        }

        // ==================== Internals ====================

        private async Task WaitIfPausedAsync(CancellationToken ct)
        {
            if (_pauseGate.IsSet) return;

            while (!_pauseGate.IsSet)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(50, ct);
            }
        }

        /// <summary>
        /// Creates batches that respect file order and API limits.
        /// Each batch contains COMPLETE files only.
        /// </summary>
        private List<DocumentBatch> CreateFileOrderedBatches(List<DocumentWithChunks> documents)
        {
            var batches = new List<DocumentBatch>();
            var currentBatch = new DocumentBatch();

            foreach (var doc in documents)
            {
                int docChunks = doc.Chunks.Count;
                int docChars = doc.Chunks.Sum(c => c.Length);

                bool wouldExceedChars = (currentBatch.TotalChars + docChars) > MAX_CHARS_PER_BATCH;
                bool wouldExceedItems = (currentBatch.TotalChunks + docChunks) > MAX_ITEMS_PER_BATCH;

                if (wouldExceedChars || wouldExceedItems)
                {
                    if (currentBatch.Documents.Count > 0)
                    {
                        batches.Add(currentBatch);
                        currentBatch = new DocumentBatch();
                    }

                    // Edge case: single document exceeds limits
                    if (docChars > MAX_CHARS_PER_BATCH || docChunks > MAX_ITEMS_PER_BATCH)
                    {
                        LogWarning($"[EmbeddingService] Document {doc.DocumentPath} exceeds single batch limits ({docChunks} chunks, {docChars} chars)");
                        var oversizedBatch = new DocumentBatch();
                        oversizedBatch.Documents.Add(doc);
                        oversizedBatch.TotalChunks = docChunks;
                        oversizedBatch.TotalChars = docChars;
                        batches.Add(oversizedBatch);
                        continue;
                    }
                }

                currentBatch.Documents.Add(doc);
                currentBatch.TotalChunks += docChunks;
                currentBatch.TotalChars += docChars;
            }

            if (currentBatch.Documents.Count > 0)
            {
                batches.Add(currentBatch);
            }

            return batches;
        }

        // ==================== Helper Classes ====================

        private class DocumentWithChunks
        {
            public string DocumentPath { get; set; }
            public List<string> Chunks { get; set; }
        }

        private class DocumentBatch
        {
            public List<DocumentWithChunks> Documents { get; set; } = new List<DocumentWithChunks>();
            public int TotalChunks { get; set; } = 0;
            public int TotalChars { get; set; } = 0;
        }
    }
}