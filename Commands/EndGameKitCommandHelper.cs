using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using VAuto.Core;
using VAuto.EndGameKit.Requests;

namespace VAuto.Commands.Core
{
    internal static class EndGameKitCommandHelper
    {
        private static object _cachedSystem;
        public static bool TryGetSystem(out object system, out string error)
        {
            system = null;
            error = string.Empty;

            // Return cached system if available
            if (_cachedSystem != null)
            {
                system = _cachedSystem;
                return true;
            }

            try
            {
                var factoryType = Type.GetType("VAuto.EndGameKit.EndGameKitSystemFactory, Vlifecycle", throwOnError: false);
                if (factoryType == null)
                {
                    error = "EndGameKitSystemFactory not available (Vlifecycle not loaded)";
                    return false;
                }

                var create = factoryType.GetMethod("Create", new[] { typeof(EntityManager), typeof(string) });
                if (create == null)
                {
                    error = "EndGameKitSystemFactory.Create not found";
                    return false;
                }

                VRCore.Initialize();
                var em = VRCore.EntityManager;
                system = create.Invoke(null, new object[] { em, null });
                if (system == null)
                {
                    error = "EndGameKitSystem creation failed";
                    return false;
                }

                // Cache the system for future use
                _cachedSystem = system;

                return true;
            }
            catch (Exception ex)
            {
                error = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
        }

        public static bool TryApplyKit(Entity character, string kitName, out string error)
        {
            error = string.Empty;

            try
            {
                var em = VRCore.EntityManager;
                if (em == default)
                {
                    error = "EntityManager not available";
                    return false;
                }

                // Create request entity with ApplyKitRequest component
                var requestEntity = em.CreateEntity();
                em.AddComponentData(requestEntity, new ApplyKitRequest
                {
                    Player = character,
                    KitName = kitName,
                    Requester = Entity.Null // No response tracking for now
                });

                // The KitRequestSystem will process this asynchronously
                return true;
            }
            catch (Exception ex)
            {
                error = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
        }

        public static bool TryRemoveKit(Entity character, out string error)
        {
            error = string.Empty;

            try
            {
                var em = VRCore.EntityManager;

                // Create request entity with RemoveKitRequest component
                var requestEntity = em.CreateEntity();
                em.AddComponentData(requestEntity, new RemoveKitRequest
                {
                    Player = character,
                    Requester = Entity.Null // No response tracking for now
                });

                // The KitRequestSystem will process this asynchronously
                return true;
            }
            catch (Exception ex)
            {
                error = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
        }

        public static bool TryLoadConfiguration(object system, out string error)
        {
            return TryInvokeBool(system, "LoadConfiguration", Array.Empty<object>(), out _, out error);
        }

        public static bool TryHasKitApplied(object system, Entity character, out bool result, out string error)
        {
            return TryInvokeBool(system, "HasKitApplied", new object[] { character }, out result, out error);
        }

        public static bool TryApplyKitForZone(object system, Entity user, Entity character, string zoneName, out string error)
        {
            return TryInvokeBool(system, "TryApplyKitForZone", new object[] { user, character, zoneName }, out _, out error);
        }

        public static List<string> GetKitProfileNames(object system)
        {
            try
            {
                var method = system.GetType().GetMethod("GetKitProfileNames", BindingFlags.Instance | BindingFlags.Public);
                if (method == null)
                    return new List<string>();

                var value = method.Invoke(system, Array.Empty<object>());
                return ToStringList(value);
            }
            catch
            {
                return new List<string>();
            }
        }

        public static object GetKitProfile(object system, string kitName)
        {
            try
            {
                var method = system.GetType().GetMethod("GetKitProfile", BindingFlags.Instance | BindingFlags.Public);
                if (method == null)
                    return null;

                return method.Invoke(system, new object[] { kitName });
            }
            catch
            {
                return null;
            }
        }

        public static bool GetBool(object profile, string propertyName, bool defaultValue = false)
        {
            return TryGetProperty(profile, propertyName, out var value) && value is bool b ? b : defaultValue;
        }

        public static int GetInt(object profile, string propertyName, int defaultValue = 0)
        {
            return TryGetProperty(profile, propertyName, out var value) && value is int i ? i : defaultValue;
        }

        public static string GetString(object profile, string propertyName, string defaultValue = "")
        {
            return TryGetProperty(profile, propertyName, out var value) ? value?.ToString() ?? defaultValue : defaultValue;
        }

        public static List<string> GetStringList(object profile, string propertyName)
        {
            return TryGetProperty(profile, propertyName, out var value) ? ToStringList(value) : new List<string>();
        }

        private static bool TryGetProperty(object target, string propertyName, out object value)
        {
            value = null;
            if (target == null)
                return false;

            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (prop == null)
                return false;

            value = prop.GetValue(target);
            return true;
        }

        private static List<string> ToStringList(object value)
        {
            var result = new List<string>();
            if (value == null)
                return result;

            if (value is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item != null)
                        result.Add(item.ToString());
                }
            }

            return result;
        }

        private static bool TryInvokeBool(object target, string methodName, object[] args, out bool result, out string error)
        {
            result = false;
            error = string.Empty;

            if (target == null)
            {
                error = "EndGameKitSystem is not available";
                return false;
            }

            try
            {
                var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
                if (method == null)
                {
                    error = $"EndGameKitSystem.{methodName} not found";
                    return false;
                }

                var value = method.Invoke(target, args);
                if (value is bool b)
                {
                    result = b;
                    return b;
                }

                error = $"{methodName} returned false";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
        }
    }
}
