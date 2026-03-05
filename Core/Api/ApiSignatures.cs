using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Entities;
using VampireCommandFramework;
using VAutomationCore.Core.Logging;

namespace VAutomationCore.Core.Api
{
    /// <summary>
    /// Centralized API signature contracts for the VAutomationCore API surface.
    /// This file is intentionally signature-only to provide a single reference point.
    /// </summary>

    public interface IServiceRegistryApi
    {
        bool RegisterSingleton<TService>(TService instance, bool replace = false) where TService : class;
        bool TryResolve<TService>(out TService service) where TService : class;
        TService GetRequired<TService>() where TService : class;
        bool IsRegistered<TService>() where TService : class;
        bool Remove<TService>() where TService : class;
        void Clear();
        int Count { get; }
        IReadOnlyCollection<Type> GetRegisteredTypes();
    }

    public interface ICoreExecutionApi
    {
        OperationResult Run(Action action, string operationName = "operation", CoreLogger? logger = null);
        OperationResult<T> Run<T>(Func<T> action, string operationName = "operation", CoreLogger? logger = null);
        OperationResult RunWithRetry(Action action, RetryPolicy? retryPolicy = null, string operationName = "operation", CoreLogger? logger = null);
        OperationResult<T> RunWithRetry<T>(Func<T> action, RetryPolicy? retryPolicy = null, string operationName = "operation", CoreLogger? logger = null);
        Task<OperationResult> RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default, string operationName = "operation", CoreLogger? logger = null);
        Task<OperationResult<T>> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default, string operationName = "operation", CoreLogger? logger = null);
    }

    public interface IEntityAliasMapperApi
    {
        bool RegisterComponentAlias<T>(string alias, bool replace = false) where T : struct;
        bool RegisterComponentAlias(string alias, Type componentType, bool replace = false);
        bool RegisterComponentAlias(string alias, string componentTypeName, bool replace = false);
        bool RemoveComponentAlias(string alias);
        bool TryResolveComponentAlias(string alias, out Type componentType);
        IReadOnlyDictionary<string, Type> GetAliases();
        bool HasComponent(EntityManager em, EntityMap entityMap, string entityAlias, string componentAlias, out bool has, out string error);
        bool TryGetComponent(EntityManager em, EntityMap entityMap, string entityAlias, string componentAlias, out object component, out string error);
        bool TrySetComponent(EntityManager em, EntityMap entityMap, string entityAlias, string componentAlias, object componentValue, out string error);
    }

    public interface IFlowServiceApi
    {
        bool RegisterFlow(string name, IEnumerable<FlowStep> steps, bool replace = false);
        bool RegisterFlow(FlowDefinition definition, bool replace = false);
        bool RegisterModuleFlows(string moduleId, IReadOnlyDictionary<string, FlowDefinition> flows, bool replace = false);
        bool UnregisterModuleFlows(string moduleId);
        bool RemoveFlow(string name);
        bool TryGetFlow(string name, out FlowDefinition definition);
        bool TryExecuteFlow(string flowId, EntityMap entityMap, out FlowExecutionResult result);
        IReadOnlyCollection<string> GetFlowNames();
        bool RegisterActionAlias(string alias, string actionName, bool replace = false);
        bool RemoveActionAlias(string alias);
        IReadOnlyDictionary<string, string> GetActionAliases();
        FlowExecutionResult Execute(string flowName, EntityMap entityMap, bool stopOnFailure = true);
        FlowExecutionResult ExecuteJobFlow(string flowName, EntityMap entityMap, ulong subjectId, bool stopOnFailure = true);
        FlowExecutionResult Execute(FlowDefinition definition, EntityMap entityMap, bool stopOnFailure = true);
    }

    public interface IConsoleRoleAuthServiceApi
    {
        void Initialize();
        bool Authenticate(ulong subjectId, string password, ConsoleRoleAuthService.ConsoleRole requestedRole, out string message);
        bool IsAuthorized(ulong subjectId, ConsoleRoleAuthService.ConsoleRole requiredRole, out TimeSpan remaining, out ConsoleRoleAuthService.ConsoleRole currentRole);
        bool CanUseJobs(ulong subjectId, out TimeSpan remaining, out ConsoleRoleAuthService.ConsoleRole currentRole);
        void Revoke(ulong subjectId);
        bool IsEnabled { get; }
    }

    public interface ICoreAuthCommandsApi
    {
        void Help(ChatCommandContext ctx);
        void LoginAdmin(ChatCommandContext ctx, string password);
        void LoginDeveloper(ChatCommandContext ctx, string password);
        void Status(ChatCommandContext ctx);
        void Logout(ChatCommandContext ctx);
    }

    public interface ICoreJobFlowCommandsApi
    {
        void Help(ChatCommandContext ctx);
        void FlowAdd(ChatCommandContext ctx, string flowName, string actionAliasOrName);
        void FlowRemove(ChatCommandContext ctx, string flowName);
        void FlowList(ChatCommandContext ctx);
        void ActionAdd(ChatCommandContext ctx, string alias, string actionName);
        void ActionRemove(ChatCommandContext ctx, string alias);
        void ActionList(ChatCommandContext ctx);
        void AliasSelf(ChatCommandContext ctx, string alias);
        void AliasUser(ChatCommandContext ctx, string alias, ulong platformId = 0);
        void AliasClear(ChatCommandContext ctx, string alias = "*");
        void AliasList(ChatCommandContext ctx);
        void ComponentAliasAdd(ChatCommandContext ctx, string alias, string componentTypeName);
        void ComponentAliasList(ChatCommandContext ctx);
        void ComponentHas(ChatCommandContext ctx, string entityAlias, string componentAlias);
        void RunFlow(ChatCommandContext ctx, string flowName, bool stopOnFailure = true);
    }
}
