using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Blueprinter.Ops
{
    [Serializable]
    public class OpAddToHangarPayload
    {
        public AssetRef BundleAsset;
        public string[] Hangars;
    }

    // Legacy Op kept for compatibility with existing Blueprinter content.
    public static class OpAddToHangarHandler
    {
        public const string OpId = "OpAddToHangar";

        private static readonly List<(string HangarKey, AircraftDefinition Aircraft)> Pending = [];

        public static void Handle(LoadedBundle bundle, OpAddToHangarPayload payload)
        {
            if (payload.BundleAsset == null || payload.Hangars == null || payload.Hangars.Length == 0)
                return;

            var aircraft = ResourcesAssetResolver.ResolveBundleAsset(bundle, payload.BundleAsset) as AircraftDefinition;
            if (!aircraft)
                return;

            foreach (var hangarKey in payload.Hangars)
            {
                if (string.IsNullOrWhiteSpace(hangarKey))
                    continue;

                Pending.Add((hangarKey, aircraft));
            }
        }

        private static string CleanName(string name) => name?.Split([" ("], 2, StringSplitOptions.None)[0] ?? string.Empty;

        [HarmonyPatch(typeof(Airbase), nameof(Airbase.AddHangar))]
        private static class AirbaseAddHangarPatch
        {
            private static void Prefix(Airbase __instance, Hangar hangar)
            {
                if (__instance.name == "airstrip_city2" && hangar.attachedUnit.UniqueName == "<MAP_UNIT>++hangar_med_10")
                    return;

                var key = $"{CleanName(hangar.attachedUnit?.name)}__{CleanName(hangar.name)}";

                foreach (var (registeredKey, aircraft) in Pending)
                {
                    if (!key.Equals(registeredKey, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (Array.IndexOf(hangar.availableAircraft, aircraft) >= 0)
                        continue;

                    hangar.availableAircraft = hangar.availableAircraft.AddToArray(aircraft);
                }
            }
        }
    }
}
