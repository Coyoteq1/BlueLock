using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using VAutomationCore.Core.Services;
using VAutomationCore.Services;

namespace VAutomationCore.Core.Api
{
    /// <summary>
    /// Alias reference resolved from an EntityMap at flow execution time.
    /// </summary>
    public readonly struct EntityAliasRef
    {
        public string Alias { get; }

        public EntityAliasRef(string alias)
        {
            Alias = alias ?? string.Empty;
        }

        public static EntityAliasRef Of(string alias)
        {
            return new EntityAliasRef(alias);
        }
    }

    /// <summary>
    /// One step in a named flow.
    /// </summary>
    public sealed class FlowStep
    {
        public string ActionAliasOrName { get; }
        public object[] Args { get; }
        public bool ContinueOnFailure { get; }

        public FlowStep(string actionAliasOrName, params object[] args)
            : this(actionAliasOrName, false, args)
        {
        }

        public FlowStep(string actionAliasOrName, bool continueOnFailure, params object[] args)
        {
            ActionAliasOrName = actionAliasOrName ?? string.Empty;
            ContinueOnFailure = continueOnFailure;
            Args = args ?? Array.Empty<object>();
        }
    }

    /// <summary>
    /// A required flow step that can be marked critical.
    /// </summary>
    public sealed class MustFlowStep
    {
        public string ActionAliasOrName { get; }
        public object[] Args { get; }
        public bool Critical { get; }

        public MustFlowStep(string actionAliasOrName, bool critical = true, params object[] args)
        {
            ActionAliasOrName = actionAliasOrName ?? string.Empty;
            Critical = critical;
            Args = args ?? Array.Empty<object>();
        }
    }

    /// <summary>
    /// Named flow definition containing ordered action steps.
    /// </summary>
    public sealed class FlowDefinition
    {
        public string Name { get; }
        public IReadOnlyList<FlowStep> Steps { get; }

        public FlowDefinition(string name, IEnumerable<FlowStep> steps)
        {
            Name = name ?? string.Empty;
            Steps = (steps ?? Array.Empty<FlowStep>()).ToArray();
        }
    }

    /// <summary>
    /// Result of flow execution.
    /// </summary>
    public readonly struct FlowExecutionResult
    {
        public bool Success { get; }
        public int TotalSteps { get; }
        public int ExecutedSteps { get; }
        public int FailedSteps { get; }
        public string ErrorMessage { get; }

        private FlowExecutionResult(bool success, int totalSteps, int executedSteps, int failedSteps, string errorMessage)
        {
            Success = success;
            TotalSteps = totalSteps;
            ExecutedSteps = executedSteps;
            FailedSteps = failedSteps;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public static FlowExecutionResult Ok(int totalSteps, int executedSteps, int failedSteps = 0)
        {
            return new FlowExecutionResult(true, totalSteps, executedSteps, failedSteps, string.Empty);
        }

        public static FlowExecutionResult Fail(int totalSteps, int executedSteps, int failedSteps, string errorMessage)
        {
            return new FlowExecutionResult(false, totalSteps, executedSteps, failedSteps, errorMessage);
        }
    }

    /// <summary>
    /// Runtime service for registering and executing action flows with entity aliases.
    /// </summary>
    public static class FlowService
    {
        private static readonly ConcurrentDictionary<string, FlowDefinition> Flows = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, string> ActionAliases = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, string> _flowModuleOwners = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> ModuleFlowIndex = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object ModuleFlowSync = new();

        /// <summary>
        /// Public accessor for flow ownership tracking (for ModuleIdRegistry validation).
        /// </summary>
        public static IReadOnlyDictionary<string, string> FlowModuleOwners => _flowModuleOwners;

        static FlowService()
        {
            RegisterDefaultActionAliases();
        }

        public static bool RegisterFlow(string name, IEnumerable<FlowStep> steps, bool replace = false)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var definition = new FlowDefinition(name.Trim(), steps);
            return RegisterFlow(definition, replace);
        }

        public static bool RegisterFlow(FlowDefinition definition, bool replace = false)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Name))
            {
                return false;
            }

            var key = definition.Name.Trim();
            if (replace)
            {
                Flows[key] = definition;
                DetachFlowOwnership(key);
                return true;
            }

            return Flows.TryAdd(key, definition);
        }

        public static bool RemoveFlow(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var key = name.Trim();
            var removed = Flows.TryRemove(key, out _);
            if (removed)
            {
                DetachFlowOwnership(key);
            }

            return removed;
        }

        public static bool TryGetFlow(string name, out FlowDefinition definition)
        {
            definition = null!;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return Flows.TryGetValue(name.Trim(), out definition);
        }

        public static IReadOnlyCollection<string> GetFlowNames()
        {
            return Flows.Keys.ToArray();
        }

        /// <summary>
        /// Registers all flows owned by a module in one operation.
        /// When replace=true, previously registered flows for the module are removed first.
        /// Compatibility note: this API is additive and does not remove existing single-flow registration methods.
        /// </summary>
        public static bool RegisterModuleFlows(string moduleId, IReadOnlyDictionary<string, FlowDefinition> flows, bool replace = false)
        {
            if (string.IsNullOrWhiteSpace(moduleId) || flows == null || flows.Count == 0)
            {
                return false;
            }

            var moduleKey = moduleId.Trim();
            lock (ModuleFlowSync)
            {
                if (replace)
                {
                    UnregisterModuleFlowsInternal(moduleKey);
                }

                // Pre-validate conflicts when replace is not requested.
                foreach (var pair in flows)
                {
                    var flowName = pair.Key?.Trim();
                    if (string.IsNullOrWhiteSpace(flowName) || pair.Value == null)
                    {
                        return false;
                    }

                    if (Flows.ContainsKey(flowName) &&
                        _flowModuleOwners.TryGetValue(flowName, out var owner) &&
                        !string.Equals(owner, moduleKey, StringComparison.OrdinalIgnoreCase) &&
                        !replace)
                    {
                        return false;
                    }
                }

                var moduleSet = ModuleFlowIndex.GetOrAdd(moduleKey, _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));
                foreach (var pair in flows)
                {
                    var flowName = pair.Key.Trim();
                    var flow = pair.Value;
                    var normalized = string.Equals(flow.Name, flowName, StringComparison.OrdinalIgnoreCase)
                        ? flow
                        : new FlowDefinition(flowName, flow.Steps);

                    Flows[flowName] = normalized;
                    _flowModuleOwners[flowName] = moduleKey;
                    moduleSet[flowName] = 0;
                }
            }

            return true;
        }

        /// <summary>
        /// Unregisters all flows owned by a module.
        /// </summary>
        public static bool UnregisterModuleFlows(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return false;
            }

            lock (ModuleFlowSync)
            {
                return UnregisterModuleFlowsInternal(moduleId.Trim());
            }
        }

        /// <summary>
        /// Tries to execute a named flow.
        /// Returns false when the flow is missing or execution fails.
        /// </summary>
        public static bool TryExecuteFlow(string flowId, EntityMap entityMap, out FlowExecutionResult result)
        {
            if (!TryGetFlow(flowId, out var definition))
            {
                result = FlowExecutionResult.Fail(0, 0, 0, $"Flow '{flowId}' not found.");
                return false;
            }

            result = Execute(definition, entityMap, stopOnFailure: true);
            return result.Success;
        }

        public static bool RegisterActionAlias(string alias, string actionName, bool replace = false)
        {
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            var key = alias.Trim();
            var value = actionName.Trim();
            if (replace)
            {
                ActionAliases[key] = value;
                return true;
            }

            return ActionAliases.TryAdd(key, value);
        }

        public static bool RemoveActionAlias(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            return ActionAliases.TryRemove(alias.Trim(), out _);
        }

        public static IReadOnlyDictionary<string, string> GetActionAliases()
        {
            return new Dictionary<string, string>(ActionAliases, StringComparer.OrdinalIgnoreCase);
        }

        public static FlowExecutionResult Execute(string flowName, EntityMap entityMap, bool stopOnFailure = true)
        {
            if (!TryGetFlow(flowName, out var definition))
            {
                return FlowExecutionResult.Fail(0, 0, 0, $"Flow '{flowName}' not found.");
            }

            return Execute(definition, entityMap, stopOnFailure);
        }

        /// <summary>
        /// Executes a flow as a job operation using role-gated authorization.
        /// Only Developer role is allowed to run jobs.
        /// </summary>
        public static FlowExecutionResult ExecuteJobFlow(string flowName, EntityMap entityMap, ulong subjectId, bool stopOnFailure = true)
        {
            if (!ConsoleRoleAuthService.CanUseJobs(subjectId, out var remaining, out var role))
            {
                var roleText = role == ConsoleRoleAuthService.ConsoleRole.None ? "none" : role.ToString();
                return FlowExecutionResult.Fail(
                    totalSteps: 0,
                    executedSteps: 0,
                    failedSteps: 0,
                    errorMessage: $"Developer authorization required for jobs. Current role: {roleText}. Remaining session: {Math.Max(0, remaining.TotalSeconds):F0}s.");
            }

            return Execute(flowName, entityMap, stopOnFailure);
        }

        public static FlowExecutionResult Execute(FlowDefinition definition, EntityMap entityMap, bool stopOnFailure = true)
        {
            return Execute(definition, entityMap, mustFlows: null, stopOnFailure);
        }

        public static FlowExecutionResult Execute(
            FlowDefinition definition,
            EntityMap entityMap,
            IEnumerable<MustFlowStep>? mustFlows,
            bool stopOnFailure = true)
        {
            if (definition == null)
            {
                return FlowExecutionResult.Fail(0, 0, 0, "Flow definition is null.");
            }

            if (entityMap == null)
            {
                return FlowExecutionResult.Fail(definition.Steps.Count, 0, 0, "Entity map is null.");
            }

            var mustFlowResult = ExecuteMustFlows(mustFlows, entityMap);
            if (!mustFlowResult.Success)
            {
                return mustFlowResult;
            }

            var executed = 0;
            var failed = 0;
            for (var i = 0; i < definition.Steps.Count; i++)
            {
                var step = definition.Steps[i];
                var actionName = ResolveActionName(step.ActionAliasOrName);
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    failed++;
                    if (stopOnFailure && !step.ContinueOnFailure)
                    {
                        return FlowExecutionResult.Fail(definition.Steps.Count, executed, failed, $"Flow step {i} has no action name.");
                    }

                    continue;
                }

                var resolvedArgs = ResolveArgs(step.Args, entityMap);
                var ok = GameActionService.InvokeAction(actionName, resolvedArgs, entityMap);
                if (!ok)
                {
                    failed++;
                    if (stopOnFailure && !step.ContinueOnFailure)
                    {
                        return FlowExecutionResult.Fail(definition.Steps.Count, executed, failed, $"Flow step {i} failed: {actionName}");
                    }
                }
                else
                {
                    executed++;
                }
            }

            return failed == 0
                ? FlowExecutionResult.Ok(definition.Steps.Count, executed)
                : FlowExecutionResult.Fail(definition.Steps.Count, executed, failed, "Flow completed with failed step(s).");
        }

        public static FlowExecutionResult ExecuteMustFlows(IEnumerable<MustFlowStep>? mustFlows, EntityMap entityMap)
        {
            if (mustFlows == null)
            {
                return FlowExecutionResult.Ok(0, 0, 0);
            }

            var steps = mustFlows as MustFlowStep[] ?? mustFlows.ToArray();
            if (steps.Length == 0)
            {
                return FlowExecutionResult.Ok(0, 0, 0);
            }

            if (entityMap == null)
            {
                return FlowExecutionResult.Fail(steps.Length, 0, 0, "Entity map is null for must-flow execution.");
            }

            var executed = 0;
            var failed = 0;
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                var actionName = ResolveActionName(step.ActionAliasOrName);
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    failed++;
                    if (step.Critical)
                    {
                        return FlowExecutionResult.Fail(steps.Length, executed, failed, $"Must-flow step {i} has no action name.");
                    }

                    continue;
                }

                var resolvedArgs = ResolveArgs(step.Args, entityMap);
                var ok = GameActionService.InvokeAction(actionName, resolvedArgs, entityMap);
                if (!ok)
                {
                    failed++;
                    if (step.Critical)
                    {
                        return FlowExecutionResult.Fail(steps.Length, executed, failed, $"Critical must-flow step {i} failed: {actionName}");
                    }
                }
                else
                {
                    executed++;
                }
            }

            return failed == 0
                ? FlowExecutionResult.Ok(steps.Length, executed)
                : FlowExecutionResult.Fail(steps.Length, executed, failed, "Must-flow execution completed with failed non-critical step(s).");
        }

        private static object[] ResolveArgs(object[] args, EntityMap entityMap)
        {
            if (args == null || args.Length == 0)
            {
                return Array.Empty<object>();
            }

            var resolved = new object[args.Length];
            for (var i = 0; i < args.Length; i++)
            {
                resolved[i] = ResolveArg(args[i], entityMap);
            }

            return resolved;
        }

        private static object ResolveArg(object arg, EntityMap entityMap)
        {
            if (arg == null)
            {
                return null!;
            }

            if (arg is EntityAliasRef entityAliasRef)
            {
                if (entityMap.TryGet(entityAliasRef.Alias, out var entity))
                {
                    return entity;
                }

                return Entity.Null;
            }

            if (arg is string str && str.Length > 1 && str[0] == '@')
            {
                var alias = str.Substring(1);
                if (entityMap.TryGet(alias, out var entity))
                {
                    return entity;
                }

                return Entity.Null;
            }

            return arg;
        }

        private static string ResolveActionName(string actionAliasOrName)
        {
            if (string.IsNullOrWhiteSpace(actionAliasOrName))
            {
                return string.Empty;
            }

            var token = actionAliasOrName.Trim();
            return ActionAliases.TryGetValue(token, out var mapped) ? mapped : token;
        }

        private static bool UnregisterModuleFlowsInternal(string moduleId)
        {
            if (!ModuleFlowIndex.TryGetValue(moduleId, out var moduleSet) || moduleSet == null || moduleSet.IsEmpty)
            {
                return false;
            }

            foreach (var flowName in moduleSet.Keys.ToArray())
            {
                if (_flowModuleOwners.TryGetValue(flowName, out var owner) &&
                    string.Equals(owner, moduleId, StringComparison.OrdinalIgnoreCase))
                {
                    Flows.TryRemove(flowName, out _);
                    _flowModuleOwners.TryRemove(flowName, out _);
                }
            }

            ModuleFlowIndex.TryRemove(moduleId, out _);
            return true;
        }

        private static void DetachFlowOwnership(string flowName)
        {
            if (string.IsNullOrWhiteSpace(flowName))
            {
                return;
            }

            if (!_flowModuleOwners.TryRemove(flowName, out var owner) || string.IsNullOrWhiteSpace(owner))
            {
                return;
            }

            if (ModuleFlowIndex.TryGetValue(owner, out var moduleSet))
            {
                moduleSet.TryRemove(flowName, out _);
                if (moduleSet.IsEmpty)
                {
                    ModuleFlowIndex.TryRemove(owner, out _);
                }
            }
        }

        private static void RegisterDefaultActionAliases()
        {
            RegisterActionAlias("apply_buff", "ApplyBuff");
            RegisterActionAlias("clean_buff", "CleanBuff");
            RegisterActionAlias("remove_buff", "RemoveBuff");
            RegisterActionAlias("teleport", "Teleport");
            RegisterActionAlias("set_position", "SetPosition");
            RegisterActionAlias("message_all", "SendMessageToAll");
            RegisterActionAlias("message_platform", "SendMessageToPlatform");
            RegisterActionAlias("message_user", "SendMessageToUser");
        }
    }
}
