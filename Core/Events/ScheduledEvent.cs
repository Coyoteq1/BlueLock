using System;
using System.Collections.Generic;

namespace VAutomationCore.Events
{
    /// <summary>
    /// Represents a scheduled event in the system.
    /// </summary>
    public class ScheduledEvent
    {
        /// <summary>
        /// The unique identifier for the event.
        /// </summary>
        public Guid Id;

        /// <summary>
        /// The name of the event.
        /// </summary>
        public string Name;

        /// <summary>
        /// The trigger type for the event.
        /// </summary>
        public EventTriggerType TriggerType;

        /// <summary>
        /// The minimum number of players required for the event to start.
        /// </summary>
        public int MinPlayers;

        /// <summary>
        /// Array of schedule entries defining when the event occurs.
        /// </summary>
        public List<ScheduleEntry> ScheduleEntries;

        /// <summary>
        /// Custom data associated with the event.
        /// </summary>
        public Dictionary<string, object> CustomData;

        /// <summary>
        /// Initializes a new instance of the ScheduledEvent class.
        /// </summary>
        public ScheduledEvent() 
        {
            CustomData = new Dictionary<string, object>();
            ScheduleEntries = new List<ScheduleEntry>();
        }

        /// <summary>
        /// Initializes a new instance of the ScheduledEvent class.
        /// </summary>
        /// <param name="id">The unique identifier for the event.</param>
        /// <param name="name">The name of the event.</param>
        /// <param name="triggerType">The trigger type for the event.</param>
        /// <param name="minPlayers">Optional minimum number of players. Defaults to 0.</param>
        public ScheduledEvent(Guid id, string name, EventTriggerType triggerType, int minPlayers = 0)
            : this()
        {
            Id = id;
            Name = name;
            TriggerType = triggerType;
            MinPlayers = minPlayers;
        }
    }

    /// <summary>
    /// Represents a scheduled time entry for an event.
    /// </summary>
    public class ScheduleEntry
    {
        /// <summary>
        /// Gets or sets the day of the week for the schedule entry. Null for daily.
        /// </summary>
        public DayOfWeek? DayOfWeek { get; set; }

        /// <summary>
        /// Gets or sets the hour (0-23) for the schedule entry.
        /// </summary>
        public int Hour { get; set; }

        /// <summary>
        /// Gets or sets the minute (0-59) for the schedule entry.
        /// </summary>
        public int Minute { get; set; }

        /// <summary>
        /// Creates a schedule entry for daily at a specific time.
        /// </summary>
        public static ScheduleEntry Daily(int hour, int minute)
        {
            return new ScheduleEntry { Hour = hour, Minute = minute };
        }

        /// <summary>
        /// Creates a schedule entry for a specific day of the week.
        /// </summary>
        public static ScheduleEntry OnDay(DayOfWeek day, int hour, int minute)
        {
            return new ScheduleEntry { DayOfWeek = day, Hour = hour, Minute = minute };
        }
    }

    /// <summary>
    /// Defines the types of triggers for scheduled events.
    /// </summary>
    public enum EventTriggerType
    {
        /// <summary>
        /// Event occurs at preset scheduled times.
        /// </summary>
        Preset,

        /// <summary>
        /// Event occurs at regular intervals.
        /// </summary>
        Interval,

        /// <summary>
        /// Event is manually triggered.
        /// </summary>
        Manual
    }

    /// <summary>
    /// Event arguments for scheduled event lifecycle.
    /// </summary>
    public class ScheduledEventArgs : EventArgs
    {
        /// <summary>
        /// The event that was triggered.
        /// </summary>
        public ScheduledEvent Event { get; }

        /// <summary>
        /// Number of players online when event started.
        /// </summary>
        public int PlayerCount { get; }

        /// <summary>
        /// Whether the event vote passed.
        /// </summary>
        public bool VotePassed { get; }

        public ScheduledEventArgs(ScheduledEvent @event, int playerCount, bool votePassed)
        {
            Event = @event;
            PlayerCount = playerCount;
            VotePassed = votePassed;
        }
    }
}
