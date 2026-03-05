using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VAutomationCore.Core.Services;

namespace VAutomationCore.Core.Services
{
    /// <summary>
    /// Event scheduling system with voting and priority management.
    /// Provides a centralized system for scheduling and managing events across the V Rising server.
    /// </summary>
    public static class EventScheduler
    {
        private static EntityManager _entityManager;
        private static bool _initialized;
        private static readonly object _initLock = new object();

        #region Event Types

        /// <summary>
        /// Types of events that can be scheduled.
        /// </summary>
        public enum EventType
        {
            ServerEvent,
            ZoneEvent,
            PlayerEvent,
            ClanEvent,
            GlobalEvent
        }

        /// <summary>
        /// Priority levels for event execution.
        /// </summary>
        public enum EventPriority
        {
            Critical = 0,
            High = 1,
            Normal = 2,
            Low = 3,
            Debug = 4
        }

        #endregion

        #region Event Configuration

        /// <summary>
        /// Configuration for event scheduling.
        /// </summary>
        public struct EventConfig
        {
            public EventType Type;
            public EventPriority Priority;
            public TimeSpan? StartTime;
            public TimeSpan? EndTime;
            public TimeSpan? Duration;
            public int? MaxOccurrences;
            public bool AllowVoting;
            public bool AutoCleanup;
            public string Category;

            public static readonly EventConfig Default = new EventConfig
            {
                Type = EventType.ServerEvent,
                Priority = EventPriority.Normal,
                AllowVoting = true,
                AutoCleanup = true,
                Category = "General"
            };
        }

        /// <summary>
        /// Event voting result.
        /// </summary>
        public struct EventVote
        {
            public Entity Voter;
            public bool VoteFor;
            public string Reason;
            public DateTime Timestamp;
        }

        #endregion

        #region Event Data Structures

        /// <summary>
        /// Event information stored in the scheduler.
        /// </summary>
        private class ScheduledEvent
        {
            public string Id;
            public string Name;
            public EventType Type;
            public EventPriority Priority;
            public Action<EventContext> Handler;
            public EventConfig Config;
            public DateTime? NextExecution;
            public int Occurrences;
            public List<EventVote> Votes;
            public bool IsActive;
            public bool IsCancelled;
            public DateTime CreatedTime;
        }

        /// <summary>
        /// Context passed to event handlers.
        /// </summary>
        public class EventContext
        {
            public string EventId;
            public string EventName;
            public EventType EventType;
            public EventPriority EventPriority;
            public DateTime ExecutionTime;
            public Entity[] AffectedEntities;
            public Dictionary<string, object> Data;
            public bool Canceled;
            public List<string> CancelReasons;
        }

        #endregion

        #region Internal Storage

        private static readonly Dictionary<string, ScheduledEvent> _events = new Dictionary<string, ScheduledEvent>();
        private static readonly Dictionary<string, List<ScheduledEvent>> _categoryIndex = new Dictionary<string, List<ScheduledEvent>>();
        private static readonly Dictionary<EventType, List<ScheduledEvent>> _typeIndex = new Dictionary<EventType, List<ScheduledEvent>>();
        private static readonly Dictionary<EventPriority, List<ScheduledEvent>> _priorityIndex = new Dictionary<EventPriority, List<ScheduledEvent>>();
        private static readonly List<ScheduledEvent> _executionQueue = new List<ScheduledEvent>();

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the event scheduler system.
        /// </summary>
        public static void Initialize()
        {
            lock (_initLock)
            {
                if (_initialized) return;

                try
                {
                    var world = World.DefaultGameObjectInjectionWorld;
                    if (world == null)
                    {
                        Plugin.Log.LogWarning("[EventScheduler] World not available, deferring initialization");
                        return;
                    }

                    _entityManager = world.EntityManager;
                    _initialized = true;
                    Plugin.Log.LogInfo("[EventScheduler] Initialized successfully");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[EventScheduler] Initialization failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Check if scheduler is ready.
        /// </summary>
        public static bool IsReady()
        {
            return _initialized && _entityManager != null;
        }

        #endregion

        #region Event Management

        /// <summary>
        /// Schedule a new event.
        /// </summary>
        /// <param name="name">Event name</param>
        /// <param name="handler">Event handler</param>
        /// <param name="config">Event configuration</param>
        /// <returns>Event ID</returns>
        public static string ScheduleEvent(
            string name,
            Action<EventContext> handler,
            EventConfig? config = null)
        {
            if (!IsReady())
            {
                Plugin.Log.LogWarning("[EventScheduler] Not initialized, cannot schedule event");
                return null;
            }

            var cfg = config ?? EventConfig.Default;
            var eventId = Guid.NewGuid().ToString();

            var scheduledEvent = new ScheduledEvent
            {
                Id = eventId,
                Name = name,
                Type = cfg.Type,
                Priority = cfg.Priority,
                Handler = handler,
                Config = cfg,
                NextExecution = cfg.StartTime.HasValue ? DateTime.UtcNow + cfg.StartTime.Value : DateTime.UtcNow,
                Occurrences = 0,
                Votes = new List<EventVote>(),
                IsActive = true,
                IsCancelled = false,
                CreatedTime = DateTime.UtcNow
            };

            lock (_events)
            {
                _events[eventId] = scheduledEvent;

                // Index by category
                if (!string.IsNullOrEmpty(cfg.Category))
                {
                    if (!_categoryIndex.ContainsKey(cfg.Category))
                        _categoryIndex[cfg.Category] = new List<ScheduledEvent>();
                    _categoryIndex[cfg.Category].Add(scheduledEvent);
                }

                // Index by type
                if (!_typeIndex.ContainsKey(cfg.Type))
                    _typeIndex[cfg.Type] = new List<ScheduledEvent>();
                _typeIndex[cfg.Type].Add(scheduledEvent);

                // Index by priority
                if (!_priorityIndex.ContainsKey(cfg.Priority))
                    _priorityIndex[cfg.Priority] = new List<ScheduledEvent>();
                _priorityIndex[cfg.Priority].Add(scheduledEvent);
            }

            Plugin.Log.LogInfo($"[EventScheduler] Scheduled event '{name}' (ID: {eventId})");
            return eventId;
        }

        /// <summary>
        /// Cancel an event.
        /// </summary>
        /// <param name="eventId">Event ID</param>
        /// <returns>True if cancelled</returns>
        public static bool CancelEvent(string eventId)
        {
            lock (_events)
            {
                if (_events.TryGetValue(eventId, out var scheduledEvent))
                {
                    scheduledEvent.IsCancelled = true;
                    _events[eventId] = scheduledEvent;
                    Plugin.Log.LogInfo($"[EventScheduler] Cancelled event '{scheduledEvent.Name}' (ID: {eventId})");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get event information.
        /// </summary>
        /// <param name="eventId">Event ID</param>
        /// <returns>Event info or null</returns>
        public static ScheduledEvent? GetEvent(string eventId)
        {
            lock (_events)
            {
                if (_events.TryGetValue(eventId, out var scheduledEvent))
                    return scheduledEvent;
            }
            return null;
        }

        /// <summary>
        /// Get all events in a category.
        /// </summary>
        /// <param name="category">Category name</param>
        /// <returns>List of events</returns>
        public static List<ScheduledEvent> GetEventsByCategory(string category)
        {
            lock (_categoryIndex)
            {
                if (_categoryIndex.TryGetValue(category, out var events))
                    return events.ToList();
            }
            return new List<ScheduledEvent>();
        }

        /// <summary>
        /// Get all events of a specific type.
        /// </summary>
        /// <param name="type">Event type</param>
        /// <returns>List of events</returns>
        public static List<ScheduledEvent> GetEventsByType(EventType type)
        {
            lock (_typeIndex)
            {
                if (_typeIndex.TryGetValue(type, out var events))
                    return events.ToList();
            }
            return new List<ScheduledEvent>();
        }

        /// <summary>
        /// Get all events with a specific priority.
        /// </summary>
        /// <param name="priority">Event priority</param>
        /// <returns>List of events</returns>
        public static List<ScheduledEvent> GetEventsByPriority(EventPriority priority)
        {
            lock (_priorityIndex)
            {
                if (_priorityIndex.TryGetValue(priority, out var events))
                    return events.ToList();
            }
            return new List<ScheduledEvent>();
        }

        #endregion

        #region Event Execution

        /// <summary>
        /// Process events that are ready to execute.
        /// </summary>
        public static void ProcessEvents()
        {
            if (!IsReady()) return;

            var now = DateTime.UtcNow;
            var eventsToExecute = new List<ScheduledEvent>();

            lock (_events)
            {
                foreach (var kvp in _events)
                {
                    var scheduledEvent = kvp.Value;
                    if (!scheduledEvent.IsActive || scheduledEvent.IsCancelled) continue;

                    if (scheduledEvent.NextExecution.HasValue && now >= scheduledEvent.NextExecution.Value)
                    {
                        eventsToExecute.Add(scheduledEvent);
                    }
                }
            }

            // Sort by priority (higher priority first)
            eventsToExecute.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            foreach (var scheduledEvent in eventsToExecute)
            {
                ExecuteEvent(scheduledEvent);
            }
        }

        /// <summary>
        /// Execute a single event.
        /// </summary>
        private static void ExecuteEvent(ScheduledEvent scheduledEvent)
        {
            try
            {
                var context = new EventContext
                {
                    EventId = scheduledEvent.Id,
                    EventName = scheduledEvent.Name,
                    EventType = scheduledEvent.Type,
                    EventPriority = scheduledEvent.Priority,
                    ExecutionTime = DateTime.UtcNow,
                    AffectedEntities = Array.Empty<Entity>(),
                    Data = new Dictionary<string, object>(),
                    Canceled = false,
                    CancelReasons = new List<string>()
                };

                // Check for voting if enabled
                if (scheduledEvent.Config.AllowVoting)
                {
                    if (!ShouldExecuteBasedOnVotes(scheduledEvent))
                    {
                        Plugin.Log.LogDebug($"[EventScheduler] Event '{scheduledEvent.Name}' did not pass voting");
                        return;
                    }
                }

                // Execute the event handler
                scheduledEvent.Handler?.Invoke(context);

                // Update event state
                lock (_events)
                {
                    if (_events.TryGetValue(scheduledEvent.Id, out var updatedEvent))
                    {
                        updatedEvent.Occurrences++;
                        updatedEvent.NextExecution = CalculateNextExecution(updatedEvent);
                        _events[scheduledEvent.Id] = updatedEvent;
                    }
                }

                Plugin.Log.LogInfo($"[EventScheduler] Executed event '{scheduledEvent.Name}' (ID: {scheduledEvent.Id})");

                // Cleanup if needed
                if (ShouldCleanupEvent(scheduledEvent))
                {
                    CancelEvent(scheduledEvent.Id);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[EventScheduler] Error executing event '{scheduledEvent.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Calculate next execution time for recurring events.
        /// </summary>
        private static DateTime? CalculateNextExecution(ScheduledEvent scheduledEvent)
        {
            if (scheduledEvent.Config.Duration.HasValue)
            {
                var endTime = scheduledEvent.CreatedTime + scheduledEvent.Config.Duration.Value;
                if (DateTime.UtcNow >= endTime)
                    return null; // No more executions
            }

            if (scheduledEvent.Config.MaxOccurrences.HasValue && scheduledEvent.Occurrences >= scheduledEvent.Config.MaxOccurrences.Value)
                return null; // Max occurrences reached

            // Default to recurring every hour
            return DateTime.UtcNow + TimeSpan.FromHours(1);
        }

        /// <summary>
        /// Determine if event should execute based on voting results.
        /// </summary>
        private static bool ShouldExecuteBasedOnVotes(ScheduledEvent scheduledEvent)
        {
            if (scheduledEvent.Votes.Count == 0) return true;

            var yesVotes = scheduledEvent.Votes.Count(v => v.VoteFor);
            var noVotes = scheduledEvent.Votes.Count(v => !v.VoteFor);

            // Simple majority voting
            return yesVotes > noVotes;
        }

        /// <summary>
        /// Determine if event should be cleaned up.
        /// </summary>
        private static bool ShouldCleanupEvent(ScheduledEvent scheduledEvent)
        {
            if (scheduledEvent.Config.AutoCleanup)
            {
                if (scheduledEvent.Config.Duration.HasValue)
                {
                    var endTime = scheduledEvent.CreatedTime + scheduledEvent.Config.Duration.Value;
                    return DateTime.UtcNow >= endTime;
                }
            }
            return false;
        }

        #endregion

        #region Voting System

        /// <summary>
        /// Cast a vote for an event.
        /// </summary>
        /// <param name="eventId">Event ID</param>
        /// <param name="voteFor">Vote for or against</param>
        /// <param name="entity">Entity casting vote</param>
        /// <param name="reason">Reason for vote</param>
        /// <returns>True if vote cast</returns>
        public static bool VoteForEvent(
            string eventId,
            bool voteFor,
            Entity entity,
            string reason = null)
        {
            lock (_events)
            {
                if (_events.TryGetValue(eventId, out var scheduledEvent))
                {
                    var existingVote = scheduledEvent.Votes.FirstOrDefault(v => v.Voter == entity);
                    if (existingVote.Voter != Entity.Null)
                    {
                        // Update existing vote
                        scheduledEvent.Votes.Remove(existingVote);
                    }

                    scheduledEvent.Votes.Add(new EventVote
                    {
                        Voter = entity,
                        VoteFor = voteFor,
                        Reason = reason,
                        Timestamp = DateTime.UtcNow
                    });

                    _events[eventId] = scheduledEvent;
                    Plugin.Log.LogInfo($"[EventScheduler] Vote cast for event '{scheduledEvent.Name}': {(voteFor ? "Yes" : "No")}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get voting results for an event.
        /// </summary>
        /// <param name="eventId">Event ID</param>
        /// <returns>Voting results</returns>
        public static (int YesVotes, int NoVotes, List<EventVote> AllVotes) GetVotingResults(string eventId)
        {
            lock (_events)
            {
                if (_events.TryGetValue(eventId, out var scheduledEvent))
                {
                    var yesVotes = scheduledEvent.Votes.Count(v => v.VoteFor);
                    var noVotes = scheduledEvent.Votes.Count(v => !v.VoteFor);
                    return (yesVotes, noVotes, scheduledEvent.Votes);
                }
            }
            return (0, 0, new List<EventVote>());
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get statistics about scheduled events.
        /// </summary>
        public static EventStatistics GetStatistics()
        {
            lock (_events)
            {
                return new EventStatistics
                {
                    TotalEvents = _events.Count,
                    ActiveEvents = _events.Count(e => e.Value.IsActive),
                    CancelledEvents = _events.Count(e => e.Value.IsCancelled),
                    EventsByCategory = _categoryIndex.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count),
                    EventsByType = _typeIndex.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.Count),
                    EventsByPriority = _priorityIndex.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.Count)
                };
            }
        }

        /// <summary>
        /// Clear all scheduled events.
        /// </summary>
        public static void ClearAllEvents()
        {
            lock (_events)
            {
                _events.Clear();
                _categoryIndex.Clear();
                _typeIndex.Clear();
                _priorityIndex.Clear();
                _executionQueue.Clear();
                Plugin.Log.LogInfo("[EventScheduler] All events cleared");
            }
        }

        #endregion
    }

    /// <summary>
    /// Event statistics for monitoring.
    /// </summary>
    public class EventStatistics
    {
        public int TotalEvents;
        public int ActiveEvents;
        public int CancelledEvents;
        public Dictionary<string, int> EventsByCategory;
        public Dictionary<string, int> EventsByType;
        public Dictionary<string, int> EventsByPriority;
    }
}