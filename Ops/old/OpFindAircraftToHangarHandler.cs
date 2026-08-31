using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Blueprinter.Ops
{
    [Serializable]
    public class OpFindAircraftToHangarPayload
    {
        public string HangarKey;
        public string[] AircraftNames;
    }

    // Legacy Op kept for compatibility with existing Blueprinter content.
    public static class OpFindAircraftToHangarHandler
    {
        public const string OpId = "OpFindAircraftToHangar";

        private static readonly List<(string HangarKey, AircraftDefinition Aircraft)> Pending = [];

        public static void Handle(LoadedBundle bundle, OpFindAircraftToHangarPayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload.HangarKey) || payload.AircraftNames == null || payload.AircraftNames.Length == 0)
                return;

            foreach (var aircraftName in payload.AircraftNames)
            {
                var aircraft = Resources.FindObjectsOfTypeAll<AircraftDefinition>().FirstOrDefault(x => x && x.name.Equals(aircraftName, StringComparison.OrdinalIgnoreCase));

                if (aircraft)
                    Pending.Add((payload.HangarKey, aircraft));
                else
                    Plugin.Log.LogWarning($"[Ops] Aircraft {aircraftName} not found");
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
