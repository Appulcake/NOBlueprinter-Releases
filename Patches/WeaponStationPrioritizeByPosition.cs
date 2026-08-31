using HarmonyLib;
using UnityEngine;

namespace Blueprinter
{
    // For Ternion
    [HarmonyPatch(typeof(WeaponStation), nameof(WeaponStation.PrioritizeByPosition))]
    public static class WeaponStationPrioritizeByPositionPatch
    {
        private static bool Prefix(Transform transform, Aircraft aircraft, ref float __result)
        {
            Vector3 p = aircraft.transform.InverseTransformPoint(transform.position);
            __result = p.x * (p.x - 0.05f) + Mathf.Abs(p.y) + Mathf.Abs(p.z);
            return false;
        }
    }
}
