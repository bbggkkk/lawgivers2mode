using System;
using System.Reflection;

namespace MelonLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class MelonInfoAttribute : Attribute
    {
        public MelonInfoAttribute(Type type, string name, string version, string author) { }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class MelonGameAttribute : Attribute
    {
        public MelonGameAttribute(string developer, string game) { }
    }

    public abstract class MelonMod
    {
        public virtual void OnInitializeMelon() { }
        public virtual void OnLateInitializeMelon() { }
        public virtual void OnUpdate() { }
        public virtual void OnSceneWasLoaded(int buildIndex, string sceneName) { }
    }

    public static class MelonLogger
    {
        public static void Msg(string value) { Console.WriteLine(value); }
        public static void Warning(string value) { Console.WriteLine("WARNING: " + value); }
        public static void Error(string value) { Console.Error.WriteLine("ERROR: " + value); }
    }
}

namespace HarmonyLib
{
    public sealed class Harmony
    {
        public Harmony(string id) { }
        public void Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null,
            HarmonyMethod transpiler = null, HarmonyMethod finalizer = null) { }
    }

    public sealed class HarmonyMethod
    {
        public HarmonyMethod(MethodInfo method) { }
    }
}
