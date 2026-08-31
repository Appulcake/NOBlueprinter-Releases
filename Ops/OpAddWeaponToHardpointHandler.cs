using System;

namespace Blueprinter.Ops
{
    [Serializable]
    public class OpAddWeaponToHardpointPayload
    {
        public string weaponJsonKey;
        public AircraftTarget[] aircraft;
    }

    [Serializable]
    public class AircraftTarget
    {
        public string aircraftJsonKey;
        public int[] hardpointIndices;
    }

    public static class OpAddWeaponToHardpointHandler
    {
        public const string OpId = "OpAddWeaponToHardpoint";

        public static void Handle(LoadedBundle _, OpAddWeaponToHardpointPayload payload)
        {
            if (Encyclopedia.WeaponLookup == null || !Encyclopedia.WeaponLookup.TryGetValue(payload.weaponJsonKey ?? string.Empty, out var mount))
            {
                Plugin.Log.LogWarning($"[Ops] Weapon {payload.weaponJsonKey} not found");
                return;
            }

            if (payload.aircraft == null || payload.aircraft.Length == 0)
            {
                Plugin.Log.LogWarning($"[Ops] No aircraft targets for {payload.weaponJsonKey}");
                return;
            }

            foreach (var target in payload.aircraft)
            {
                if (target == null || string.IsNullOrWhiteSpace(target.aircraftJsonKey))
                {
                    Plugin.Log.LogWarning("[Ops] Invalid aircraft target");
                    continue;
                }

                if (target.hardpointIndices == null || target.hardpointIndices.Length == 0)
                {
                    Plugin.Log.LogWarning($"[Ops] No hardpoints for {target.aircraftJsonKey}");
                    continue;
                }

                if (Encyclopedia.Lookup == null || !Encyclopedia.Lookup.TryGetValue(target.aircraftJsonKey, out var unit) || unit is not AircraftDefinition aircraft)
                {
                    Plugin.Log.LogWarning($"[Ops] Aircraft {target.aircraftJsonKey} not found");
                    continue;
                }

                var weaponManager = aircraft.unitPrefab?.GetComponentInChildren<WeaponManager>(true);
                if (weaponManager == null)
                {
                    Plugin.Log.LogWarning($"[Ops] Aircraft {target.aircraftJsonKey} has no weapon manager");
                    continue;
                }

                if (weaponManager.hardpointSets == null)
                {
                    Plugin.Log.LogWarning($"[Ops] Weapon manager {weaponManager.name} has no hardpoints");
                    continue;
                }

                foreach (var hardpointIndex in target.hardpointIndices)
                {
                    if (hardpointIndex < 0 || hardpointIndex >= weaponManager.hardpointSets.Length)
                    {
                        Plugin.Log.LogWarning($"[Ops] Invalid hardpoint index {hardpointIndex} on {weaponManager.name}");
                        continue;
                    }

                    var set = weaponManager.hardpointSets[hardpointIndex];
                    if (set == null)
                    {
                        Plugin.Log.LogWarning($"[Ops] Hardpoint {hardpointIndex} is null on {weaponManager.name}");
                        continue;
                    }

                    set.weaponOptions ??= [];
                    if (!set.weaponOptions.Contains(mount))
                        set.weaponOptions.Add(mount);
                }
            }
        }
    }
}
