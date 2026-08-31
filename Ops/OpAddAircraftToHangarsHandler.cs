using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Blueprinter.Ops
{
    [Serializable]
    public class OpAddAircraftToHangarsPayload
    {
        public string aircraftJsonKey;
        public HangarTarget[] hangars;
    }

    [Serializable]
    public class HangarTarget
    {
        public string hangarUnitJsonKey;
        public string[] hangarNames;
    }

    public static class OpAddAircraftToHangarsHandler
    {
        public const string OpId = "OpAddAircraftToHangars";

        private static readonly List<(string UnitJsonKey, string HangarName, AircraftDefinition Aircraft)> Pending = [];

        public static void Handle(LoadedBundle _, OpAddAircraftToHangarsPayload payload)
        {
            if (Encyclopedia.Lookup == null || !Encyclopedia.Lookup.TryGetValue(payload.aircraftJsonKey ?? string.Empty, out var unit) || unit is not AircraftDefinition aircraft)
            {
                Plugin.Log.LogWarning($"[Ops] Aircraft {payload.aircraftJsonKey} not found");
                return;
            }

            if (payload.hangars == null || payload.hangars.Length == 0)
            {
                Plugin.Log.LogWarning($"[Ops] No hangar targets for {payload.aircraftJsonKey}");
                return;
            }

            foreach (var target in payload.hangars)
            {
                if (target == null || string.IsNullOrWhiteSpace(target.hangarUnitJsonKey))
                {
                    Plugin.Log.LogWarning("[Ops] Invalid hangar target");
                    continue;
                }

                if (target.hangarNames == null || target.hangarNames.Length == 0)
                {
                    Plugin.Log.LogWarning($"[Ops] No hangars for {target.hangarUnitJsonKey}");
                    continue;
                }

                foreach (var hangarName in target.hangarNames)
                {
                    if (string.IsNullOrWhiteSpace(hangarName))
                    {
                        Plugin.Log.LogWarning($"[Ops] Invalid hangar for {target.hangarUnitJsonKey}");
                        continue;
                    }

                    Pending.Add((target.hangarUnitJsonKey, hangarName, aircraft));
                }
            }
        }

        private static string CleanName(string name) => name?.Split([" ("], 2, StringSplitOptions.None)[0] ?? string.Empty;

        [HarmonyPatch(typeof(Airbase), nameof(Airbase.AddHangar))]
        private static class AirbaseAddHangarPatch
        {
            private static void Prefix(Airbase __instance, Hangar hangar)
            {
                if (hangar?.attachedUnit?.definition == null)
                    return;

                if (__instance.name == "airstrip_city2" && hangar.attachedUnit.UniqueName == "<MAP_UNIT>++hangar_med_10")
                    return;

                var unitJsonKey = hangar.attachedUnit.definition.jsonKey;
                var hangarName = CleanName(hangar.name);

                foreach (var (registeredUnitJsonKey, registeredHangarName, aircraft) in Pending)
                {
                    if (!string.Equals(unitJsonKey, registeredUnitJsonKey, StringComparison.Ordinal) || !string.Equals(hangarName, registeredHangarName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (Array.IndexOf(hangar.availableAircraft, aircraft) >= 0)
                        continue;

                    hangar.availableAircraft = hangar.availableAircraft.AddToArray(aircraft);
                }
            }
        }
    }
}
