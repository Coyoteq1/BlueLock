using Unity.Entities;
using VAutomationCore.Core.Api;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.Lifecycle
{
    public interface IFlowLifecycle
    {
        void ExecuteEnterFlow(string flowId, string zoneId, Entity player);
        void ExecuteExitFlow(string flowId, string zoneId, Entity player);
    }

    public class FlowLifecycleManager : IFlowLifecycle
    {
        private static readonly CoreLogger Log = new CoreLogger("FlowLifecycleManager");

        public void ExecuteEnterFlow(string flowId, string zoneId, Entity player)
        {
            _ = TryExecuteEnterFlow(flowId, zoneId, player);
        }

        public void ExecuteExitFlow(string flowId, string zoneId, Entity player)
        {
            _ = TryExecuteExitFlow(flowId, zoneId, player);
        }

        public LifecycleExecutionResult TryExecuteEnterFlow(string flowId, string zoneId, Entity player)
        {
            if (string.IsNullOrEmpty(flowId))
            {
                return LifecycleExecutionResult.Ok("Enter flow skipped: empty flowId.");
            }
            var flowKey = flowId.Trim();
            if (!FlowService.TryGetFlow(flowKey, out var definition))
            {
                Log.Warning("Flow not found: " + flowKey);
                return LifecycleExecutionResult.Fail(LifecycleExecutionFailureCode.MissingFlow, "Enter flow not found: " + flowKey);
            }

            var entityMap = new EntityMap();
            entityMap.SetEntity("player", player);
            entityMap.SetString("zoneId", zoneId ?? string.Empty);
            var result = FlowService.Execute(definition, entityMap, stopOnFailure: false);
            Log.Info("Enter flow executed: " + flowKey + " Result: " + result.Success);
            return result.Success
                ? LifecycleExecutionResult.Ok("Enter flow executed: " + flowKey)
                : LifecycleExecutionResult.Fail(LifecycleExecutionFailureCode.RuntimeActionFailure, result.ErrorMessage ?? "Enter flow execution failed.");
        }

        public LifecycleExecutionResult TryExecuteExitFlow(string flowId, string zoneId, Entity player)
        {
            if (string.IsNullOrEmpty(flowId))
            {
                return LifecycleExecutionResult.Ok("Exit flow skipped: empty flowId.");
            }

            var flowKey = flowId.Trim();
            if (!FlowService.TryGetFlow(flowKey, out var definition))
            {
                Log.Warning("Flow not found: " + flowKey);
                return LifecycleExecutionResult.Fail(LifecycleExecutionFailureCode.MissingFlow, "Exit flow not found: " + flowKey);
            }

            var entityMap = new EntityMap();
            entityMap.SetEntity("player", player);
            entityMap.SetString("zoneId", zoneId ?? string.Empty);
            var result = FlowService.Execute(definition, entityMap, stopOnFailure: false);
            Log.Info("Exit flow executed: " + flowKey + " Result: " + result.Success);
            return result.Success
                ? LifecycleExecutionResult.Ok("Exit flow executed: " + flowKey)
                : LifecycleExecutionResult.Fail(LifecycleExecutionFailureCode.RuntimeActionFailure, result.ErrorMessage ?? "Exit flow execution failed.");
        }
    }
}
