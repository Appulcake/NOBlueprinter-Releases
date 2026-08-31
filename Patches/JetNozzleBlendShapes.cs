using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Blueprinter
{
    [HarmonyPatch(typeof(JetNozzle), nameof(JetNozzle.Thrust))]
    public static class JetNozzleBlendShapes
    {
        private static readonly ConditionalWeakTable<JetNozzle, SkinnedMeshRenderer> Cache = new ConditionalWeakTable<JetNozzle, SkinnedMeshRenderer>();
        private static void Postfix(JetNozzle __instance, float thrustAmount, float rpmRatio, float thrustRatio, float throttle, bool allowAfterburner)
        {
            if (!__instance)
                return;
            if (!Cache.TryGetValue(__instance, out var rend) || !rend)
            {
                rend = __instance.GetComponent<SkinnedMeshRenderer>();
                if (!rend)
                    return;
                Cache.Remove(__instance);
                Cache.Add(__instance, rend);
            }

            if (!rend.sharedMesh || rend.sharedMesh.blendShapeCount == 0)
                return;

            float targetPos = thrustRatio * 100f;
            float currentPos = rend.GetBlendShapeWeight(0);

            if (throttle > 0.9f && allowAfterburner)
                targetPos = 0f;

            float nozzlePosition = currentPos < targetPos ? Mathf.Min(currentPos + 1f, targetPos) : Mathf.Max(currentPos - 1f, targetPos);

            rend.SetBlendShapeWeight(0, nozzlePosition);
        }
    }
}
