using System;
using Unity.Entities;
using VAuto.Core;
using VAuto.Core.Lifecycle.Snapshots;
using VAuto.Core.Lifecycle.Snapshots.Sections;
using ProjectM.Network;
using VAuto.Services.Interfaces;
using VAuto.Core.Chat;

namespace VAuto.Core.Lifecycle
{
    public sealed class SnapshotLifecycleService : IArenaLifecycleService, IService
    {
        public string Name => "SnapshotLifecycleService";
        public bool IsInitialized { get; private set; }
        public BepInEx.Logging.ManualLogSource Log { get; private set; }

        private readonly SnapshotStore _store = new();
        private readonly InventorySectionSaver _inventorySaver = new();
        private readonly EquipmentSectionSaver _equipmentSaver = new();
        private readonly JewelSocketSectionSaver _jewelSaver = new();
        private readonly SpellbookSectionSaver _spellbookSaver = new();
        private readonly BuffGlowSectionSaver _buffSaver = new();
        private readonly VBloodSectionSaver _vbloodSaver = new();
        private LifecycleStepsConfig _steps = new();

        public SnapshotLifecycleService()
        {
            Log = Plugin.Log;
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            _steps = LifecycleStepsPolicy.Load();
            IsInitialized = true;
        }

        public void Cleanup() { }

        public bool OnPlayerEnter(Entity user, Entity character, string arenaId)
        {
            Initialize();
            _steps = LifecycleStepsPolicy.Load();
            if (!_steps.SaveSnapshotOnEnter) return true;
            var em = VRCore.EntityManager;
            var platformId = GetPlatformId(em, user, character);
            var snap = new CharacterSnapshot
            {
                ArenaId = arenaId,
                PlatformId = platformId,
                CreatedUtc = DateTime.UtcNow
            };

            _inventorySaver.Capture(character, snap, em);
            _equipmentSaver.Capture(character, snap, em);
            _jewelSaver.Capture(character, snap, em);
            _spellbookSaver.Capture(character, snap, em);
            _buffSaver.Capture(character, snap, em);
            _vbloodSaver.Capture(character, snap, em);

            _store.Save(platformId, arenaId, snap);
            return true;
        }

        public bool OnPlayerExit(Entity user, Entity character, string arenaId)
        {
            Initialize();
            _steps = LifecycleStepsPolicy.Load();
            if (!_steps.RestoreSnapshotOnExit) return true;
            var em = VRCore.EntityManager;
            var platformId = GetPlatformId(em, user, character);
            if (!_store.TryLoad(platformId, arenaId, out var snap)) return true;

            if (_steps.ClearInventoryOnRestore)
            {
                _inventorySaver.Restore(character, new CharacterSnapshot(), em); // clear pass
                _equipmentSaver.Restore(character, new CharacterSnapshot(), em);
                _jewelSaver.Restore(character, new CharacterSnapshot(), em);
            }

            _inventorySaver.Restore(character, snap, em);
            _equipmentSaver.Restore(character, snap, em);
            _jewelSaver.Restore(character, snap, em);
            _spellbookSaver.Reset();
            _spellbookSaver.Restore(character, snap, em);
            _buffSaver.Restore(character, snap, em);
            _vbloodSaver.Restore(character, snap, em);

            var requiresSpellbookRebuild = _spellbookSaver.RequiresRebuild;

            if (_steps.OpenSpellbookUi && requiresSpellbookRebuild)
            {
                NotifySpellbookMenu(em, user, arenaId);
            }

            _store.Delete(platformId, arenaId);
            return true;
        }

        public bool OnArenaStart(string arenaId) => true;
        public bool OnArenaEnd(string arenaId) => true;

        private static ulong GetPlatformId(EntityManager em, Entity user, Entity character)
        {
            try
            {
                if (user != Entity.Null && em.Exists(user) && em.HasComponent<User>(user))
                {
                    return em.GetComponentData<User>(user).PlatformId;
                }
            }
            catch { }

            return 0;
        }

        private void NotifySpellbookMenu(EntityManager em, Entity user, string arenaId)
        {
            try
            {
                if (user == Entity.Null || em == default || !em.Exists(user) || !em.HasComponent<User>(user))
                    return;

                var platformId = em.GetComponentData<User>(user).PlatformId;
                var message = $"Spellbook slots may need manual review after arena '{arenaId}'. Open the spellbook menu to refresh your loadout.";
                ChatService.TrySendSystemMessage(platformId, message, out _);
            }
            catch
            {
                // best effort
            }
        }

    }
}
