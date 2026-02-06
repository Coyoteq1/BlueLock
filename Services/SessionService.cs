using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectM;
using ProjectM.Network;

namespace VAuto.Core.Services
{
    /// <summary>
    /// Service for managing user session history during arena/lifecycle events.
    /// Tracks enter/exit sessions with events and state snapshots.
    /// </summary>
    public class SessionService
    {
        private readonly Dictionary<Entity, PlayerSessionData> sessions = new Dictionary<Entity, PlayerSessionData>();
        private readonly object lockObj = new object();

        public void StartSession(Entity userEntity, Entity characterEntity, string sessionType)
        {
            lock (lockObj)
            {
                if (sessions.ContainsKey(userEntity))
                {
                    Plugin.Log.LogWarning($"[Session] Session already exists for user {userEntity}, ending it first.");
                    EndSession(userEntity);
                }

                var session = new PlayerSessionData
                {
                    UserEntity = userEntity,
                    CharacterEntity = characterEntity,
                    SessionType = sessionType,
                    StartTime = DateTime.UtcNow,
                    Events = new List<SessionEvent>()
                };

                // Capture initial state
                CaptureStateSnapshot(userEntity, characterEntity, session);

                sessions[userEntity] = session;
                Plugin.Log.LogInfo($"[Session] Started {sessionType} session for user {userEntity}");
            }
        }

        public void EndSession(Entity userEntity)
        {
            lock (lockObj)
            {
                if (!sessions.TryGetValue(userEntity, out var session))
                {
                    Plugin.Log.LogWarning($"[Session] No session found for user {userEntity}");
                    return;
                }

                session.EndTime = DateTime.UtcNow;
                session.Duration = session.EndTime - session.StartTime;

                Plugin.Log.LogInfo($"[Session] Ended {session.SessionType} session. Duration: {session.Duration.TotalSeconds:F1}s, Events: {session.Events.Count}");

                // Log all events
                foreach (var evt in session.Events)
                {
                    Plugin.Log.LogInfo($"[Session]   - {evt.Timestamp:HH:mm:ss}: {evt.EventType} - {evt.Description}");
                }

                sessions.Remove(userEntity);
            }
        }

        public void AddEvent(Entity userEntity, string eventType, string description)
        {
            lock (lockObj)
            {
                if (!sessions.TryGetValue(userEntity, out var session))
                {
                    Plugin.Log.LogWarning($"[Session] Cannot add event - no session for user {userEntity}");
                    return;
                }

                session.Events.Add(new SessionEvent
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = eventType,
                    Description = description
                });
            }
        }

        public PlayerSessionData GetSession(Entity userEntity)
        {
            lock (lockObj)
            {
                sessions.TryGetValue(userEntity, out var session);
                return session;
            }
        }

        public bool IsInSession(Entity userEntity, string sessionType = null)
        {
            lock (lockObj)
            {
                if (!sessions.TryGetValue(userEntity, out var session))
                    return false;

                if (sessionType == null)
                    return true;

                return session.SessionType == sessionType;
            }
        }

        public List<PlayerSessionData> GetAllSessions()
        {
            lock (lockObj)
            {
                return sessions.Values.ToList();
            }
        }

        private void CaptureStateSnapshot(Entity userEntity, Entity characterEntity, PlayerSessionData session)
        {
            try
            {
                if (userEntity == Entity.Null || !VRCore.EntityManager.Exists(userEntity))
                    return;

                var user = VRCore.EntityManager.GetComponentData<User>(userEntity);
                
                session.UserData = new UserStateData
                {
                    PlatformId = user.PlatformId,
                    CharacterName = user.CharacterName.ToString(),
                    IsConnected = user.IsConnected
                };

                if (characterEntity != Entity.Null && VRCore.EntityManager.Exists(characterEntity))
                {
                    var position = float3.zero;
                    if (VRCore.EntityManager.HasComponent<LocalTransform>(characterEntity))
                    {
                        position = VRCore.EntityManager.GetComponentData<LocalTransform>(characterEntity).Position;
                    }
                    else if (VRCore.EntityManager.HasComponent<Translation>(characterEntity))
                    {
                        position = VRCore.EntityManager.GetComponentData<Translation>(characterEntity).Value;
                    }

                    session.CharacterData = new CharacterStateData
                    {
                        Position = position,
                        TerritoryIndex = GetTerritoryIndex(position)
                    };
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Session] Failed to capture state snapshot: {ex.Message}");
            }
        }

        private int GetTerritoryIndex(float3 position)
        {
            const float gridSize = 100f;
            var xIndex = (int)math.floor(position.x / gridSize);
            var zIndex = (int)math.floor(position.z / gridSize);
            return xIndex * 1000 + zIndex;
        }
    }

    public class PlayerSessionData
    {
        public Entity UserEntity { get; set; }
        public Entity CharacterEntity { get; set; }
        public string SessionType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public UserStateData UserData { get; set; }
        public CharacterStateData CharacterData { get; set; }
        public List<SessionEvent> Events { get; set; }
    }

    public class UserStateData
    {
        public ulong PlatformId { get; set; }
        public string CharacterName { get; set; }
        public bool IsConnected { get; set; }
    }

    public class CharacterStateData
    {
        public float3 Position { get; set; }
        public int TerritoryIndex { get; set; }
    }

    public class SessionEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; }
        public string Description { get; set; }
    }
}
