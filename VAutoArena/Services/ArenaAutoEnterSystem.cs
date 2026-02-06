using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using VAuto.Core;

namespace VAuto.Arena.Services
{
    internal sealed class ArenaAutoEnterSystem : SystemBase
    {
        private EntityQuery _playerQuery;

        public override void OnCreate()
        {
            // Create entity query for player characters
            _playerQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ProjectM.PlayerCharacter>());
        }

        public override void OnUpdate()
        {
            if (!ArenaAutoEnterSettings.AutoEnterEnabled && !ArenaAutoEnterSettings.AutoExitEnabled)
                return;

            var radius = ArenaPlayerService.ArenaRadius;
            if (radius <= 0f)
                return;

            var center = ArenaPlayerService.ArenaCenter;
            
            // Get player entities
            var entities = _playerQuery.ToEntityArray(Allocator.Temp);
            
            try
            {
                foreach (var character in entities)
                {
                    if (!ArenaPlayerService.TryGetCharacterPosition(character, out var pos))
                        continue;

                    var inZone = ArenaPlayerService.IsInZone(pos, center, radius);
                    var inArena = ArenaPlayerService.IsPlayerInArena(character);

                    if (inZone && !inArena && ArenaAutoEnterSettings.AutoEnterEnabled)
                    {
                        ArenaPlayerService.ManualEnterArena(character, out _);
                    }
                    else if (!inZone && inArena && ArenaAutoEnterSettings.AutoExitEnabled)
                    {
                        ArenaPlayerService.ManualExitArena(character, out _);
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }
        }
    }
}
