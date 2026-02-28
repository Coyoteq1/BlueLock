using Unity.Entities;
using VAutomationCore.Core.Api;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.Lifecycle
{
    public interface IFlowLifecycle
    {
        void ExecuteEnterFlow(string flowId, Entity player);
        void ExecuteExitFlow(string flowId, Entity player);
    }

    public class FlowLifecycleManager : IFlowLifecycle
    {
        private static readonly CoreLogger Log = new CoreLogger("FlowLifecycleManager");

        public void ExecuteEnterFlow(string flowId, Entity player)
        {
            if (string.IsNullOrEmpty(flowId)) return;
            var flowKey = flowId.Trim();
            if (!FlowService.TryGetFlow(flowKey, out var definition))
            {
                Log.Warning("Flow not found: " + flowKey);
                return;
            }

            var entityMap = new EntityMap();
            entityMap.Map("player", player);
            var result = FlowService.Execute(definition, entityMap, stopOnFailure: false);
            Log.Info("Enter flow executed: " + flowKey + " Result: " + result.Success);
        }

        public void ExecuteExitFlow(string flowId, Entity player)
        {
            if (string.IsNullOrEmpty(flowId)) return;
            var flowKey = flowId.Trim();
            if (!FlowService.TryGetFlow(flowKey, out var definition))
            {
                Log.Warning("Flow not found: " + flowKey);
                return;
            }

            var entityMap = new EntityMap();
            entityMap.Map("player", player);
            var result = FlowService.Execute(definition, entityMap, stopOnFailure: false);
            Log.Info("Exit flow executed: " + flowKey + " Result: " + result.Success);
        }
    }
}
