using System;
using System.Collections.Generic;
using System.Reflection;

namespace LobotomyArchipelago
{
    /// <summary>
    /// Applies Harmony patches through reflection, so the mod works with the Harmony 1.x bundled with the game
    /// (Harmony.HarmonyInstance) as well as Harmony 2 (HarmonyLib.Harmony) shipped by newer mod loaders.
    /// </summary>
    public static class HarmonyBridge
    {
        private static object _harmony;
        private static MethodInfo _patch;
        private static ConstructorInfo _harmonyMethodCtor;

        public static readonly List<string> Report = new List<string>();

        public static bool Init(string id)
        {
            Assembly harmonyAssembly = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name == "0Harmony" || name == "HarmonyX" || name == "0HarmonyX")
                {
                    harmonyAssembly = assembly;
                    break;
                }
            }
            if (harmonyAssembly == null)
            {
                try
                {
                    harmonyAssembly = Assembly.Load("0Harmony");
                }
                catch (Exception e)
                {
                    Log.Error("Harmony is not available", e);
                    return false;
                }
            }

            Type legacy = harmonyAssembly.GetType("Harmony.HarmonyInstance");
            Type modern = harmonyAssembly.GetType("HarmonyLib.Harmony");
            Type harmonyMethod = harmonyAssembly.GetType("Harmony.HarmonyMethod") ??
                                 harmonyAssembly.GetType("HarmonyLib.HarmonyMethod");
            if (harmonyMethod == null)
            {
                Log.Error("HarmonyMethod type not found in " + harmonyAssembly.FullName);
                return false;
            }
            _harmonyMethodCtor = harmonyMethod.GetConstructor(new[] { typeof(MethodInfo) });

            if (legacy != null)
            {
                _harmony = legacy.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { id });
                _patch = FindPatchMethod(legacy, harmonyMethod);
            }
            else if (modern != null)
            {
                _harmony = Activator.CreateInstance(modern, id);
                _patch = FindPatchMethod(modern, harmonyMethod);
            }

            if (_harmony == null || _patch == null || _harmonyMethodCtor == null)
            {
                Log.Error("Unsupported Harmony version: " + harmonyAssembly.FullName);
                return false;
            }
            Log.Info("Using " + harmonyAssembly.FullName);
            return true;
        }

        private static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethod)
        {
            foreach (MethodInfo method in harmonyType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name != "Patch") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length >= 3 && parameters[0].ParameterType == typeof(MethodBase) &&
                    parameters[1].ParameterType == harmonyMethod)
                {
                    return method;
                }
            }
            return null;
        }

        /// <summary>Patches typeName.methodName with static methods of <see cref="Patches"/>.</summary>
        public static bool Patch(string typeName, string methodName, int parameterCount, string prefix, string postfix)
        {
            string label = typeName + "." + methodName;
            try
            {
                Type type = Reflect.Type(typeName);
                if (type == null)
                {
                    Report.Add("MISSING TYPE  " + label);
                    return false;
                }
                MethodInfo original = Reflect.Method(type, methodName, parameterCount);
                if (original == null)
                {
                    Report.Add("MISSING METHOD " + label);
                    Log.Warn("Cannot patch " + label + ": method not found");
                    return false;
                }
                ParameterInfo[] patchParameters = _patch.GetParameters();
                var args = new object[patchParameters.Length];
                args[0] = original;
                args[1] = prefix == null ? null : HarmonyMethodFor(prefix);
                args[2] = postfix == null ? null : HarmonyMethodFor(postfix);
                for (int i = 3; i < args.Length; i++) args[i] = null;
                _patch.Invoke(_harmony, args);
                Report.Add("ok            " + label);
                return true;
            }
            catch (Exception e)
            {
                Report.Add("FAILED        " + label + " (" + (e.InnerException ?? e).Message + ")");
                Log.Error("Failed to patch " + label, e.InnerException ?? e);
                return false;
            }
        }

        private static object HarmonyMethodFor(string name)
        {
            MethodInfo method = typeof(Patches).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic |
                                                                BindingFlags.Static);
            if (method == null) throw new MissingMethodException("Patches", name);
            return _harmonyMethodCtor.Invoke(new object[] { method });
        }
    }
}
