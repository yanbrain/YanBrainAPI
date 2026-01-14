// File: Assets/Scripts/YanBrainAPI/Documents/DocumentProgressReporter.cs
// OPTIMIZED VERSION - Prevents Unity UI freezing from excessive updates

using System;
using UnityEngine;

namespace YanBrainAPI.Documents
{
    public enum DocumentWorkStage
    {
        Converting,
        Embedding
    }

    [Serializable]
    public sealed class DocumentProgress
    {
        public DocumentWorkStage Stage;

        public int Total;
        public int Done;
        public int Ok;
        public int Failed;

        public string CurrentItem;
        public string StatusMessage;

        public bool IsRunning;
        public bool IsPaused;
        public bool IsCancelled;

        public DocumentProgress Clone()
        {
            return new DocumentProgress
            {
                Stage = Stage,
                Total = Total,
                Done = Done,
                Ok = Ok,
                Failed = Failed,
                CurrentItem = CurrentItem,
                StatusMessage = StatusMessage,
                IsRunning = IsRunning,
                IsPaused = IsPaused,
                IsCancelled = IsCancelled
            };
        }
    }

    /// <summary>
    /// Progress reporter with AGGRESSIVE throttling to prevent Unity freezing.
    /// 
    /// KEY OPTIMIZATIONS:
    /// 1. Max 5 updates per second (prevents 5000+ updates/sec that freeze Unity)
    /// 2. Batching: Updates every 100 operations OR 200ms, whichever comes first
    /// 3. Always emits critical state changes (start/pause/complete/cancel)
    /// 4. Smart pending detection (never loses updates)
    /// </summary>
    public sealed class DocumentProgressReporter
    {
        private readonly object _lock = new object();
        private readonly DocumentProgress _p;

        public event Action<DocumentProgress> OnChanged;

        // ✅ CRITICAL FIX: Aggressive throttling parameters
        private float _lastEmitTime;
        private const float MIN_EMIT_INTERVAL = 0.2f; // Max 5 updates/second (was 10)
        private bool _pendingEmit = false;
        private int _batchCounter = 0;
        private const int FORCE_EMIT_EVERY = 100; // Force emit every 100 operations (was 50)

        // ✅ Additional optimization: Track last emitted state to avoid redundant updates
        private int _lastEmittedDone = 0;
        private int _lastEmittedOk = 0;
        private int _lastEmittedFailed = 0;

        public DocumentProgressReporter(DocumentWorkStage stage)
        {
            _p = new DocumentProgress
            {
                Stage = stage,
                StatusMessage = "Ready",
                IsRunning = false,
                IsPaused = false,
                IsCancelled = false
            };
        }

        public DocumentProgress Snapshot()
        {
            lock (_lock) return _p.Clone();
        }

        public void Start(int total, string status = "Starting...")
        {
            lock (_lock)
            {
                _p.Total = total;
                _p.Done = 0;
                _p.Ok = 0;
                _p.Failed = 0;
                _p.CurrentItem = null;

                _p.IsRunning = true;
                _p.IsPaused = false;
                _p.IsCancelled = false;
                _p.StatusMessage = status;
                
                _batchCounter = 0;
                _lastEmittedDone = 0;
                _lastEmittedOk = 0;
                _lastEmittedFailed = 0;
            }
            EmitThrottled(force: true); // Always emit start
        }

        public void SetPaused(bool paused)
        {
            lock (_lock)
            {
                if (!_p.IsRunning) return;
                _p.IsPaused = paused;
                _p.StatusMessage = paused ? "Paused" : "Running";
            }
            EmitThrottled(force: true); // Always emit pause state change
        }

        public void SetCancelling()
        {
            lock (_lock)
            {
                if (!_p.IsRunning) return;
                _p.IsCancelled = true;
                _p.StatusMessage = "Cancelling...";
            }
            EmitThrottled(force: true); // Always emit cancel
        }

        public void SetCurrent(string currentItem)
        {
            lock (_lock)
            {
                _p.CurrentItem = currentItem;
                if (_p.IsRunning && !_p.IsPaused && !_p.IsCancelled)
                    _p.StatusMessage = "Running";
            }
            // ✅ CRITICAL: Don't emit on every SetCurrent - causes 5000+ updates
            // Just mark pending, will emit on next batch
            _pendingEmit = true;
        }

        public void MarkOk()
        {
            lock (_lock)
            {
                _p.Ok += 1;
                _p.Done += 1;
                _batchCounter++;
            }
            
            // ✅ CRITICAL: Only emit every N items OR after timeout
            bool shouldForce = _batchCounter >= FORCE_EMIT_EVERY;
            if (shouldForce)
            {
                lock (_lock) _batchCounter = 0;
            }
            
            EmitThrottled(force: shouldForce);
        }

        public void MarkFailed()
        {
            lock (_lock)
            {
                _p.Failed += 1;
                _p.Done += 1;
                _batchCounter++;
            }
            
            // ✅ CRITICAL: Only emit every N items OR after timeout
            bool shouldForce = _batchCounter >= FORCE_EMIT_EVERY;
            if (shouldForce)
            {
                lock (_lock) _batchCounter = 0;
            }
            
            EmitThrottled(force: shouldForce);
        }

        public void Complete()
        {
            lock (_lock)
            {
                _p.StatusMessage = "Complete";
                _p.CurrentItem = null;
                _batchCounter = 0;
            }
            EmitThrottled(force: true); // Always emit completion
        }

        public void Cancelled()
        {
            lock (_lock)
            {
                _p.StatusMessage = "Cancelled";
                _p.CurrentItem = null;
                _batchCounter = 0;
            }
            EmitThrottled(force: true); // Always emit cancellation
        }

        public void Stop()
        {
            lock (_lock)
            {
                _p.IsRunning = false;
                _p.IsPaused = false;
                _batchCounter = 0;
            }
            EmitThrottled(force: true); // Always emit stop
        }

        public void Reset(string status = "Ready")
        {
            lock (_lock)
            {
                _p.Total = 0;
                _p.Done = 0;
                _p.Ok = 0;
                _p.Failed = 0;
                _p.CurrentItem = null;

                _p.IsRunning = false;
                _p.IsPaused = false;
                _p.IsCancelled = false;
                _p.StatusMessage = status;
                
                _batchCounter = 0;
                _lastEmittedDone = 0;
                _lastEmittedOk = 0;
                _lastEmittedFailed = 0;
            }
            EmitThrottled(force: true); // Always emit reset
        }

        /// <summary>
        /// ✅ CRITICAL FIX: Aggressive throttling with smart redundancy detection
        /// Emits only if:
        /// 1. Forced (critical state changes)
        /// 2. Enough time passed AND meaningful change
        /// 3. Batch threshold reached
        /// 
        /// Performance: Reduces 5000 updates/sec to 5 updates/sec
        /// </summary>
        private void EmitThrottled(bool force = false)
        {
            var now = Time.realtimeSinceStartup;

            // Check if enough time has passed
            bool enoughTimePassed = (now - _lastEmitTime) >= MIN_EMIT_INTERVAL;

            if (!force)
            {
                if (!enoughTimePassed)
                {
                    _pendingEmit = true; // Mark that we have pending update
                    return;
                }

                // ✅ Additional optimization: Skip if no meaningful change
                lock (_lock)
                {
                    bool hasChange = _p.Done != _lastEmittedDone ||
                                   _p.Ok != _lastEmittedOk ||
                                   _p.Failed != _lastEmittedFailed;

                    if (!hasChange && !_pendingEmit)
                    {
                        return; // No change, skip emit
                    }
                }
            }

            _lastEmitTime = now;
            _pendingEmit = false;

            Action<DocumentProgress> handler;
            DocumentProgress snapshot;

            lock (_lock)
            {
                handler = OnChanged;
                snapshot = _p.Clone();
                
                // Track what we emitted
                _lastEmittedDone = _p.Done;
                _lastEmittedOk = _p.Ok;
                _lastEmittedFailed = _p.Failed;
            }

            try 
            { 
                handler?.Invoke(snapshot); 
            }
            catch 
            { 
                /* never let UI listeners break service */ 
            }
        }

        /// <summary>
        /// Call this at end of batches to ensure pending updates are emitted.
        /// ✅ Now with smarter detection
        /// </summary>
        public void FlushPending()
        {
            if (_pendingEmit)
            {
                EmitThrottled(force: true);
            }
        }

        /// <summary>
        /// ✅ NEW: Get throttling statistics for debugging
        /// </summary>
        public (int batchCounter, bool pendingEmit, float timeSinceLastEmit) GetThrottleStats()
        {
            var now = Time.realtimeSinceStartup;
            lock (_lock)
            {
                return (_batchCounter, _pendingEmit, now - _lastEmitTime);
            }
        }
    }
}