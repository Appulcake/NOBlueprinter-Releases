using System;
using System.Collections.Generic;

namespace Blueprinter.Ops
{
    [Serializable]
    public class OpAddToEncyclopediaPayload
    {
        public AssetRef[] entries;
    }

    public static class EncyclopediaLoader
    {
        public const string OpId = "OpAddToEncyclopedia";

        public static void Load(Encyclopedia encyclopedia, IReadOnlyList<LoadedBundle> bundles)
        {
            var unitKeys = new HashSet<string>(StringComparer.Ordinal);
            var weaponKeys = new HashSet<string>(StringComparer.Ordinal);
            if (Encyclopedia.Lookup != null)
                unitKeys.UnionWith(Encyclopedia.Lookup.Keys);
            if (Encyclopedia.WeaponLookup != null)
                weaponKeys.UnionWith(Encyclopedia.WeaponLookup.Keys);

            var definitions = new List<INetworkDefinition>();
            foreach (var bundle in bundles)
                Collect(bundle, definitions, unitKeys, weaponKeys);

            if (definitions.Count == 0)
                return;

            definitions.Sort((a, b) => { var comparison = string.CompareOrdinal(GetJsonKey(a), GetJsonKey(b)); return comparison != 0 ? comparison : string.CompareOrdinal(a.GetType().FullName, b.GetType().FullName); });
            Encyclopedia.Lookup ??= new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
            Encyclopedia.WeaponLookup ??= new Dictionary<string, WeaponMount>(StringComparer.Ordinal);
            encyclopedia.IndexLookup ??= new List<INetworkDefinition>();

            foreach (var definition in definitions)
            {
                if (definition is UnitDefinition unit)
                    AddUnit(encyclopedia, unit);
                else
                    AddWeaponMount(encyclopedia, (WeaponMount)definition);

                definition.LookupIndex = encyclopedia.IndexLookup.Count;
                encyclopedia.IndexLookup.Add(definition);
            }

            Plugin.Log.LogDebug($"[EncyclopediaLoader] Added {definitions.Count} entries");
        }

        private static void Collect(LoadedBundle bundle, List<INetworkDefinition> definitions, HashSet<string> unitKeys, HashSet<string> weaponKeys)
        {
            if (bundle?.Manifest?.Ops == null)
                return;

            foreach (var op in bundle.Manifest.Ops)
            {
                if (op?.opId != OpId)
                    continue;

                if (string.IsNullOrEmpty(op.payloadJson))
                {
                    Plugin.Log.LogWarning($"[EncyclopediaLoader] Empty payload in {bundle.bundleName}");
                    continue;
                }

                OpAddToEncyclopediaPayload payload;
                try
                {
                    payload = JsonUtilities.Deserialize<OpAddToEncyclopediaPayload>(op.payloadJson);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[EncyclopediaLoader] Invalid payload in {bundle.bundleName}");
                    Plugin.Log.LogWarning(ex);
                    continue;
                }

                if (payload?.entries == null || payload.entries.Length == 0)
                {
                    Plugin.Log.LogWarning($"[EncyclopediaLoader] Empty entries in {bundle.bundleName}");
                    continue;
                }

                foreach (var entry in payload.entries)
                {
                    if (entry == null)
                    {
                        Plugin.Log.LogWarning($"[EncyclopediaLoader] Null asset reference in {bundle.bundleName}");
                        continue;
                    }

                    var obj = ResourcesAssetResolver.ResolveBundleAsset(bundle, entry);
                    if (obj == null)
                        continue;

                    if (obj is not UnitDefinition && obj is not WeaponMount)
                    {
                        Plugin.Log.LogWarning($"[EncyclopediaLoader] Unsupported type {obj.GetType().Name} for {obj.name}");
                        continue;
                    }

                    var definition = (INetworkDefinition)obj;
                    var jsonKey = GetJsonKey(definition);
                    if (string.IsNullOrWhiteSpace(jsonKey))
                    {
                        Plugin.Log.LogWarning($"[EncyclopediaLoader] Missing JSON key on {obj.name}");
                        continue;
                    }

                    var keys = definition is UnitDefinition ? unitKeys : weaponKeys;
                    if (!keys.Add(jsonKey))
                    {
                        Plugin.Log.LogWarning($"[EncyclopediaLoader] Duplicate {obj.GetType().Name} JSON key {jsonKey} on {obj.name}");
                        continue;
                    }

                    definitions.Add(definition);
                }
            }
        }

        private static void AddUnit(Encyclopedia encyclopedia, UnitDefinition unit)
        {
            switch (unit)
            {
                case AircraftDefinition aircraft:
                    encyclopedia.aircraft.Add(aircraft);
                    break;
                case VehicleDefinition vehicle:
                    encyclopedia.vehicles.Add(vehicle);
                    break;
                case MissileDefinition missile:
                    encyclopedia.missiles.Add(missile);
                    break;
                case BuildingDefinition building:
                    encyclopedia.buildings.Add(building);
                    break;
                case ShipDefinition ship:
                    encyclopedia.ships.Add(ship);
                    break;
                case SceneryDefinition scenery:
                    encyclopedia.scenery.Add(scenery);
                    break;
                default:
                    encyclopedia.otherUnits.Add(unit);
                    break;
            }

            try
            {
                unit.CacheMass();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[EncyclopediaLoader] CacheMass failed for {unit.name}");
                Plugin.Log.LogWarning(ex);
            }

            Encyclopedia.Lookup.Add(GetJsonKey(unit), unit);
        }

        private static void AddWeaponMount(Encyclopedia encyclopedia, WeaponMount mount)
        {
            encyclopedia.weaponMounts.Add(mount);

            try
            {
                mount.Initialize();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[EncyclopediaLoader] Initialize failed for {mount.name}");
                Plugin.Log.LogWarning(ex);
            }

            Encyclopedia.WeaponLookup.Add(GetJsonKey(mount), mount);
        }

        private static string GetJsonKey(INetworkDefinition definition)
        {
            if (definition is IHasJsonKey hasKey && !string.IsNullOrWhiteSpace(hasKey.JsonKey))
                return hasKey.JsonKey;
            if (definition is UnitDefinition unit)
                return unit.jsonKey;
            if (definition is WeaponMount mount)
                return mount.jsonKey;
            return null;
        }
    }
}
