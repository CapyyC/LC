using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace LobotomyArchipelago
{
    /// <summary>
    /// Cached reflection helpers. The mod does not reference the game's Assembly-CSharp at compile time so it keeps
    /// working across game/BaseMod versions; every member is looked up by name (with fallbacks) at runtime.
    /// </summary>
    public static class Reflect
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                         BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        private static Assembly _gameAssembly;
        private static Dictionary<string, Type> _typesByName;
        private static readonly Dictionary<string, MemberInfo> MemberCache = new Dictionary<string, MemberInfo>();
        private static readonly Dictionary<string, MethodInfo> MethodCache = new Dictionary<string, MethodInfo>();
        private static readonly HashSet<string> Reported = new HashSet<string>();

        public static Assembly GameAssembly
        {
            get
            {
                if (_gameAssembly != null) return _gameAssembly;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "Assembly-CSharp")
                    {
                        _gameAssembly = assembly;
                        break;
                    }
                }
                return _gameAssembly;
            }
        }

        /// <summary>Finds a game type by full name or simple name.</summary>
        public static Type Type(string name)
        {
            if (_typesByName == null)
            {
                _typesByName = new Dictionary<string, Type>();
                Assembly game = GameAssembly;
                if (game == null) return null;
                Type[] types;
                try
                {
                    types = game.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = Array.FindAll(e.Types, t => t != null);
                }
                foreach (Type type in types)
                {
                    if (type.FullName != null) _typesByName[type.FullName] = type;
                    if (!_typesByName.ContainsKey(type.Name)) _typesByName[type.Name] = type;
                }
            }
            Type result;
            if (_typesByName.TryGetValue(name, out result)) return result;
            ReportOnce("type:" + name, "Game type not found: " + name);
            return null;
        }

        public static void ReportOnce(string key, string message)
        {
            if (Reported.Add(key)) Log.Warn(message);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Fields and properties
        // ------------------------------------------------------------------------------------------------------------

        private static MemberInfo FindMember(Type type, string name)
        {
            string key = type.FullName + "::" + name;
            MemberInfo member;
            if (MemberCache.TryGetValue(key, out member)) return member;
            for (Type t = type; t != null && member == null; t = t.BaseType)
            {
                member = (MemberInfo)t.GetField(name, All | BindingFlags.DeclaredOnly)
                         ?? t.GetProperty(name, All | BindingFlags.DeclaredOnly);
            }
            MemberCache[key] = member;
            return member;
        }

        /// <summary>Reads the first existing field or property among the given names.</summary>
        public static object Get(object target, params string[] names)
        {
            if (target == null) return null;
            Type type = target as Type ?? target.GetType();
            object instance = target is Type ? null : target;
            foreach (string name in names)
            {
                MemberInfo member = FindMember(type, name);
                if (member == null) continue;
                try
                {
                    var field = member as FieldInfo;
                    if (field != null) return field.GetValue(instance);
                    var property = (PropertyInfo)member;
                    if (property.GetIndexParameters().Length == 0) return property.GetValue(instance, null);
                }
                catch (Exception e)
                {
                    ReportOnce("get:" + type.Name + "." + name, "Could not read " + type.Name + "." + name + ": " +
                                                                 e.Message);
                }
            }
            ReportOnce("get:" + type.Name + ":" + string.Join("/", names),
                "None of the members " + string.Join(", ", names) + " exist on " + type.Name);
            return null;
        }

        public static bool Has(object target, string name)
        {
            if (target == null) return false;
            Type type = target as Type ?? target.GetType();
            return FindMember(type, name) != null;
        }

        /// <summary>Writes the first existing field or property among the given names.</summary>
        public static bool Set(object target, object value, params string[] names)
        {
            if (target == null) return false;
            Type type = target as Type ?? target.GetType();
            object instance = target is Type ? null : target;
            foreach (string name in names)
            {
                MemberInfo member = FindMember(type, name);
                if (member == null) continue;
                try
                {
                    var field = member as FieldInfo;
                    if (field != null)
                    {
                        field.SetValue(instance, Coerce(value, field.FieldType));
                        return true;
                    }
                    var property = (PropertyInfo)member;
                    if (property.CanWrite)
                    {
                        property.SetValue(instance, Coerce(value, property.PropertyType), null);
                        return true;
                    }
                }
                catch (Exception e)
                {
                    ReportOnce("set:" + type.Name + "." + name, "Could not write " + type.Name + "." + name + ": " +
                                                                 e.Message);
                }
            }
            return false;
        }

        public static float GetFloat(object target, params string[] names)
        {
            object value = Get(target, names);
            return value == null ? 0f : Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        public static int GetInt(object target, params string[] names)
        {
            object value = Get(target, names);
            if (value == null) return 0;
            if (value is Enum) return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public static long GetLong(object target, params string[] names)
        {
            object value = Get(target, names);
            return value == null ? 0L : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Gets the singleton of a game manager class (static "instance", "Instance", ...).</summary>
        public static object Singleton(string typeName)
        {
            Type type = Type(typeName);
            if (type == null) return null;
            foreach (string name in new[] { "instance", "Instance", "_instance", "currentGameManager", "Window",
                                            "CurrentWindow", "currentWindow", "instnace" })
            {
                MemberInfo member = FindMember(type, name);
                if (member == null) continue;
                var field = member as FieldInfo;
                if (field != null && field.IsStatic) return field.GetValue(null);
                var property = member as PropertyInfo;
                if (property != null && property.GetGetMethod(true) != null && property.GetGetMethod(true).IsStatic)
                {
                    return property.GetValue(null, null);
                }
            }
            ReportOnce("singleton:" + typeName, "No singleton accessor found on " + typeName);
            return null;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Methods
        // ------------------------------------------------------------------------------------------------------------

        public static MethodInfo Method(Type type, string name, int parameterCount = -1)
        {
            if (type == null) return null;
            string key = type.FullName + "::" + name + "/" + parameterCount;
            MethodInfo method;
            if (MethodCache.TryGetValue(key, out method)) return method;
            for (Type t = type; t != null && method == null; t = t.BaseType)
            {
                foreach (MethodInfo candidate in t.GetMethods(All | BindingFlags.DeclaredOnly))
                {
                    if (candidate.Name != name) continue;
                    if (parameterCount >= 0 && candidate.GetParameters().Length != parameterCount) continue;
                    method = candidate;
                    break;
                }
            }
            MethodCache[key] = method;
            return method;
        }

        /// <summary>
        /// Calls an instance (or static, when target is a Type) method by name. The overload is chosen by argument
        /// count and compatibility; ints are converted to enums, floats, longs... as needed.
        /// </summary>
        public static object Call(object target, string name, params object[] args)
        {
            if (target == null) return null;
            Type type = target as Type ?? target.GetType();
            object instance = target is Type ? null : target;
            args = args ?? new object[0];
            MethodInfo method = FindOverload(type, name, args);
            if (method == null)
            {
                ReportOnce("call:" + type.Name + "." + name + "/" + args.Length,
                    "Method not found: " + type.Name + "." + name + " with " + args.Length + " argument(s)");
                return null;
            }
            ParameterInfo[] parameters = method.GetParameters();
            var converted = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                converted[i] = i < args.Length ? Coerce(args[i], parameters[i].ParameterType)
                    : (parameters[i].IsOptional ? parameters[i].DefaultValue : null);
            }
            return method.Invoke(instance, converted);
        }

        /// <summary>Like <see cref="Call"/> but swallows and logs exceptions.</summary>
        public static object TryCall(object target, string name, params object[] args)
        {
            try
            {
                return Call(target, name, args);
            }
            catch (Exception e)
            {
                Type type = target as Type ?? (target == null ? null : target.GetType());
                ReportOnce("trycall:" + (type == null ? "?" : type.Name) + "." + name,
                    "Calling " + (type == null ? "?" : type.Name) + "." + name + " failed: " +
                    (e.InnerException ?? e).Message);
                return null;
            }
        }

        /// <summary>Picks the best overload: exact type matches beat conversions (e.g. GetSefira(string) vs (SefiraEnum)).</summary>
        private static MethodInfo FindOverload(Type type, string name, object[] args)
        {
            MethodInfo best = null;
            int bestScore = -1;
            for (Type t = type; t != null; t = t.BaseType)
            {
                foreach (MethodInfo candidate in t.GetMethods(All | BindingFlags.DeclaredOnly))
                {
                    if (candidate.Name != name || candidate.IsGenericMethodDefinition) continue;
                    ParameterInfo[] parameters = candidate.GetParameters();
                    int required = 0;
                    foreach (ParameterInfo p in parameters) if (!p.IsOptional) required++;
                    if (args.Length < required || args.Length > parameters.Length) continue;
                    int score = parameters.Length == args.Length ? 1 : 0;
                    for (int i = 0; i < args.Length && score >= 0; i++)
                    {
                        int match = MatchScore(args[i], parameters[i].ParameterType);
                        score = match < 0 ? -1 : score + match;
                    }
                    if (score > bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }
            }
            return best;
        }

        /// <summary>-1: incompatible, 1: needs a conversion, 3: directly assignable.</summary>
        private static int MatchScore(object arg, Type parameterType)
        {
            if (parameterType.IsByRef) return -1;
            if (arg == null) return parameterType.IsValueType ? -1 : 3;
            Type argType = arg.GetType();
            if (parameterType.IsAssignableFrom(argType)) return 3;
            if (parameterType.IsEnum) return arg is int || arg is long || argType.IsEnum ? 1 : -1;
            if (IsNumeric(parameterType) && (IsNumeric(argType) || argType.IsEnum)) return 1;
            return -1;
        }

        private static bool IsNumeric(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double) ||
                   type == typeof(short) || type == typeof(byte) || type == typeof(uint) || type == typeof(ulong);
        }

        public static object Coerce(object value, Type targetType)
        {
            if (value == null || targetType.IsInstanceOfType(value)) return value;
            if (targetType.IsEnum)
            {
                if (value is string) return Enum.Parse(targetType, (string)value, true);
                return Enum.ToObject(targetType, Convert.ToInt64(value, CultureInfo.InvariantCulture));
            }
            if (IsNumeric(targetType)) return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return value;
        }

        /// <summary>Converts a game enum value by name (e.g. "MIDNIGHT" of type OrdealLevel).</summary>
        public static object EnumValue(string typeName, string valueName)
        {
            Type type = Type(typeName);
            if (type == null || !type.IsEnum) return null;
            try
            {
                return Enum.Parse(type, valueName, true);
            }
            catch (Exception)
            {
                ReportOnce("enum:" + typeName + "." + valueName, "Enum value not found: " + typeName + "." + valueName);
                return null;
            }
        }

        public static IEnumerable Enumerate(object value)
        {
            var enumerable = value as IEnumerable;
            if (enumerable == null || value is string) yield break;
            foreach (object element in enumerable) yield return element;
        }

        public static List<object> ToList(object value)
        {
            var list = new List<object>();
            foreach (object element in Enumerate(value)) list.Add(element);
            return list;
        }

        public static bool IsAlive(object obj)
        {
            if (obj == null) return false;
            var unityObject = obj as UnityEngine.Object;
            if (unityObject != null || obj is UnityEngine.Object) return unityObject != null;
            return true;
        }
    }
}
