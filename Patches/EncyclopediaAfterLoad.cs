using System;
using HarmonyLib;

namespace Blueprinter
{
    //load entrypoint
    [HarmonyPatch(typeof(Encyclopedia),nameof(Encyclopedia.AfterLoad), new Type[0])]
    public static class EncyclopediaAfterLoadPatch
    {
        private static bool RunOnce;

        private static void Postfix(Encyclopedia __instance)
        {
            if (RunOnce)
                return;
            RunOnce = true;
            Plugin.Instance.StartCoroutine(Plugin.Instance.RunRoutine(__instance));
        }
    }
}
