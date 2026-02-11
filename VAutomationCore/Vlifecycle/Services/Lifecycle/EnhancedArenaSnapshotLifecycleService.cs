using System;
using System.Collections.Generic;
using BepInEx.Logging;
using ProjectM.Network;
using Unity.Entities;
using Unity.Mathematics;
using VAuto.Core.Lifecycle;

namespace VAuto.Core.Lifecycle
{
    /// <summary>
    /// Lifecycle service for EnhancedArenaSnapshotService.
    /// Handles snapshot creation on arena entry and deletion on arena exit.
    /// </summary>
    public class EnhancedArenaSnapshotLifecycleService : IArenaLifecycleService
    {
        public bool IsInitialized { get; private set; }
        public ManualLogSource Log { get; private set; }
        
        private readonly Dictionary<Entity, string> _playerArenaMap = new();
        private readonly Dictionary<Entity, Entity> _userCharacterMap = new();

        /// <summary>
        /// Initialize the snapshot lifecycle service
        /// </summary>
        public void Initialize(ManualLogSource logger)
        {
            try
            {
                Log = logger;
                IsInitialized = true;
                Log?.LogInfo("[EnhancedArenaSnapshotLifecycleService] Initialized snapshot lifecycle service");
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnhancedArenaSnapshotLifecycleService] Failed to initialize: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Cleanup the snapshot lifecycle service
        /// </summary>
        public void Cleanup()
        {
            try
            {
                _playerArenaMap.Clear();
                _userCharacterMap.Clear();
                IsInitialized = false;
                Log?.LogInfo("[EnhancedArenaSnapshotLifecycleService] Cleaned up snapshot lifecycle service");
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnhancedArenaSnapshotLifecycleService] Failed to cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when player enters arena - CREATE SNAPSHOT
        /// </summary>
        public bool OnPlayerEnter(Entity user, Entity character, string arenaId)
        {
            try
            {
                if (!ValidateState())
                {
                    Log?.LogWarning("[EnhancedArenaSnapshotLifecycleService] Service not initialized, cannot handle player enter");
                    return false;
                }

                Log?.LogInfo($"[EnhancedArenaSnapshotLifecycleService] Player entering arena {arenaId} - creating snapshot");

                // Store arena mapping for exit
                _playerArenaMap[user] = arenaId;
                _userCharacterMap[user] = character;

                // Create snapshot when player enters (async)
                var success = AsyncCreateSnapshot(user, character, arenaId);
                
                if (success)
                {
                    Log?.LogInfo($"[EnhancedArenaSnapshotLifecycleService] ✅ Snapshot created successfully for arena entry");
                }
                else
                {
                    Log?.LogWarning($"[EnhancedArenaSnapshotLifecycleService] ❌ Failed to create snapshot for arena entry");
                }

                return success;
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnhancedArenaSnapshotLifecycleService] Error in OnPlayerEnter: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Called when player exits arena - DELETE SNAPSHOT
        /// </summary>
        public bool OnPlayerExit(Entity user, Entity character, string arenaId)
        {
            try
            {
                if (!ValidateState())
                {
                    Log?.LogWarning("[EnhancedArenaSnapshotLifecycleService] Service not initialized, cannot handle player exit");
                    return true; // Exit should not fail
                }

                // Use stored arenaId if available
                if (_playerArenaMap.TryGetValue(user, out var storedArenaId))
                {
                    arenaId = storedArenaId;
                    _playerArenaMap.Remove(user);
                }

                // Get character from stored mapping if not provided
                if (character == Entity.Null && _userCharacterMap.TryGetValue(user, out var storedCharacter))
                {
                    character = storedCharacter;
                    _userCharacterMap.Remove(user);
                }

                Log?.LogInfo($"[EnhancedArenaSnapshotLifecycleService] Player exiting arena {arenaId} - deleting snapshot");

                // Get character ID for snapshot deletion
                var characterId = GetCharacterId(user);

                // Delete snapshot when player exits (async)
                AsyncDeleteSnapshot(characterId, arenaId);
                
                Log?.LogInfo($"[EnhancedArenaSnapshotLifecycleService] ✅ Snapshot deletion initiated for arena exit");
                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnhancedArenaSnapshotLifecycleService] Error in OnPlayerExit: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Called when arena lifecycle starts
        /// </summary>
        public bool OnArenaStart(string arenaId)
        {
            try
            {
                if (!ValidateState()) return true;
                
                Log?.LogInfo($"[EnhancedArenaSnapshotLifecycleService] Arena {arenaId} started - no action needed for snapshots");
                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnhancedArenaSnapshotLifecycleService] Error in OnArenaStart: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Called when arena lifecycle ends
        /// </summary>
        public bool OnArenaEnd(string arenaId)
        {
            try
            {
                if (!ValidateState()) return true;
                
                Log?.LogInfo($"[EnhancedArenaSnapshotLifecycleService] Arena {arenaId} ended - cleaning up all snapshots");
                
                // Clean up all snapshots for this arena
                CleanupArenaSnapshots(arenaId);
                
                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnhancedArenaSnapshotLifecycleService] Error in OnArenaEnd: {ex.Message}");
                return false;
            }
        }

        #region Private Helper Methods

        private bool ValidateState()
        {
            return IsInitialized && Log != null;
        }

        private string GetCharacterId(Entity user)
        {
            try
            {
                var em = VAutoCore.EntityManager;
                if (em == null)
                {
                    Log?.LogWarning("[EnhancedArenaSnapshotLifecycleService] EntityManager not available");
                    return user.Index.ToString();
                }

                if (em.TryGetComponentData(user, out User userData))
                {
                    return userData.PlatformId.ToString();
                }

                Log?.LogWarning("[EnhancedArenaSnapshotLifecycleService] Could not get user data");
                return user.Index.ToString();
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnhancedArenaSnapshotLifecycleService] Error getting character ID: {ex.Message}");
                return user.Index.ToString();
            }
        }

        private bool AsyncCreateSnapshot(Entity user, Entity character, string arenaId)
        {
            // TODO: Replace with actual EnhancedArenaSnapshotService.CreateSnapshot call
            // This is a placeholder for the async implementation
            try
            {
                // Example: return EnhancedArenaSnapshotService.CreateSnapshotAsync(user, character, arenaId).Result;
                
                // For now, create a PlayerLifecycleEvent and log
                var lifecycleEvent = new PlayerLifecycleEvent
                {
                    UserEntity = user,
                    CharacterEntity = character,
                    ArenaId = arenaId,
                    EventType = PlayerLifecycleEventType.Enter,
                    Timestamp = DateTime.UtcNow,
                    EventData = { { "Method", "AsyncCreateSnapshot" } }
                };
                
                Log?.LogInfo($"[EnhancedArenaSnapshotLifecycleService] Created event for arena entry snapshot");
                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnhancedArenaSnapshotLifecycleService] AsyncCreateSnapshot failed: {ex.Message}");
                return false;
            }
        }

        private void AsyncDeleteSnapshot(string characterId, string arenaId)
        {
            // TODO: Replace with actual EnhancedArenaSnapshotService.DeleteSnapshotAsync call
            // This is a placeholder for the async implementation
            try
            {
                // Example: EnhancedArenaSnapshotService.DeleteSnapshotAsync(characterId, arenaId);
                
                Log?.LogInfo($"[EnhancedArenaSnapshotLifecycleService] Initiated async delete for snapshot: character={characterId}, arena={arenaId}");
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnhancedArenaSnapshotLifecycleService] AsyncDeleteSnapshot failed: {ex.Message}");
            }
        }

        private void CleanupArenaSnapshots(string arenaId)
        {
            // TODO: Implement cleanup of all snapshots for the arena
            try
            {
                Log?.LogInfo($"[EnhancedArenaSnapshotLifecycleService] Cleanup initiated for arena: {arenaId}");
                // Implementation should delete all snapshots associated with the arena
            }
            catch (Exception ex)
            {
                Log?.LogError($"[EnhancedArenaSnapshotLifecycleService] CleanupArenaSnapshots failed: {ex.Message}");
            }
        }

        #endregion
    }
}
