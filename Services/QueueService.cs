using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Task execution queue for managing arena-related tasks.
    /// Uses improved threading with Task.Run for better IL2CPP compatibility.
    /// </summary>
    public class QueueService : IDisposable
    {
        private readonly Queue<ArenaTask> _taskQueue = new Queue<ArenaTask>();
        private readonly object _lock = new object();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _workerTask;
        private bool _isRunning = false;

        /// <summary>
        /// Initializes and starts the queue service.
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            _cts.CancelAfter(Timeout.Infinite);
            _workerTask = Task.Factory.StartNew(
                ProcessQueue, 
                TaskCreationOptions.LongRunning);
            
            Plugin.Log.LogInfo("[QueueService] Started with dedicated thread");
        }

        /// <summary>
        /// Adds a task to the queue.
        /// </summary>
        public void EnqueueTask(ArenaTask task)
        {
            lock (_lock)
            {
                _taskQueue.Enqueue(task);
                Monitor.Pulse(_lock);
            }
        }

        /// <summary>
        /// Adds a task with immediate execution (bypasses queue).
        /// </summary>
        public void ExecuteImmediate(ArenaTask task)
        {
            try
            {
                task.Execute();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[QueueService] Immediate execution error: {ex.Message}");
            }
        }

        private void ProcessQueue()
        {
            while (!_cts.Token.IsCancellationRequested && _isRunning)
            {
                ArenaTask task = null;
                
                lock (_lock)
                {
                    while (_taskQueue.Count == 0 && _isRunning)
                    {
                        if (!Monitor.Wait(_lock, 1000))
                        {
                            // Timeout - check if we should continue
                            if (!_isRunning) break;
                        }
                    }
                    
                    if (_taskQueue.Count > 0)
                    {
                        task = _taskQueue.Dequeue();
                    }
                }
                
                if (task != null)
                {
                    try
                    {
                        task.Execute();
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError($"[QueueService] Task execution error: {ex.Message}");
                    }
                }
            }
            
            Plugin.Log.LogInfo("[QueueService] Worker thread stopped");
        }

        /// <summary>
        /// Gets the current queue count.
        /// </summary>
        public int QueueCount
        {
            get
            {
                lock (_lock)
                {
                    return _taskQueue.Count;
                }
            }
        }

        /// <summary>
        /// Stops the queue service gracefully.
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            
            lock (_lock)
            {
                Monitor.Pulse(_lock);
            }
            
            try
            {
                _workerTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Task was cancelled
            }
            
            _cts.Cancel();
            Plugin.Log.LogInfo("[QueueService] Stopped");
        }

        /// <summary>
        /// Clears all pending tasks.
        /// </summary>
        public void ClearQueue()
        {
            lock (_lock)
            {
                _taskQueue.Clear();
            }
            Plugin.Log.LogInfo("[QueueService] Queue cleared");
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }
    }

    /// <summary>
    /// Abstract base class for arena tasks.
    /// </summary>
    public abstract class ArenaTask
    {
        /// <summary>
        /// Unique task identifier.
        /// </summary>
        public string TaskId { get; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// Priority level (higher = processed first).
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Timestamp when task was created.
        /// </summary>
        public DateTime CreatedAt { get; } = DateTime.UtcNow;

        /// <summary>
        /// Executes the task.
        /// </summary>
        public abstract void Execute();

        /// <summary>
        /// Optional cleanup logic.
        /// </summary>
        public virtual void Cleanup() { }
    }
}
