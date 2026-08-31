
using HarmonyLib;
using UnityEngine;

namespace Blueprinter
{
    //SetCockpitRenderers NULL checking
    [HarmonyPatch(typeof(Aircraft), nameof(Aircraft.SetCockpitRenderers))]
    public static class AircraftSetCockpitRenderersPatch
    {
        private static bool Prefix(Aircraft __instance, bool enabled)
        {
            if (__instance.cockpitRenderers == null || __instance.exteriorRenderers == null)
                return true;

            foreach (Renderer cockpitRenderer in __instance.cockpitRenderers)
            {
                if (cockpitRenderer == null)
                    continue;
                cockpitRenderer.enabled = enabled;
            }

            foreach (Renderer exteriorRenderer in __instance.exteriorRenderers)
            {
                if (exteriorRenderer == null)
                    continue;
                exteriorRenderer.enabled = !enabled;
            }

            foreach (IEngine engine in __instance.engines)
            {
                engine.SetInteriorSounds(enabled);
            }
            return false;
        }
    }
}
