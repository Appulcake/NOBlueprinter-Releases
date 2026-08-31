using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NuclearOption.SavedMission;
using UnityEngine;
using static NuclearOption.SavedMission.MissionGroup;

namespace Blueprinter.Ops
{
    [Serializable]
    public class OpAddMissionsPayload
    {
        public AssetRef[] MissionAssets;
        public string[] MissionGroups;
    }

    public static class OpAddMissionsHandler
    {
        public const string OpId = "OpAddMissions";

        private static readonly Dictionary<string, ResourceGroup> resourceGroups = [];
        // ResourceGroup arrays are readonly, so replacing them requires reflection even with Assembly-CSharp publicized.
        private static readonly FieldInfo AssetsField = AccessTools.Field(typeof(ResourceGroup), "assets");
        private static readonly FieldInfo NamesField = AccessTools.Field(typeof(ResourceGroup), "names");
        public static void Handle(LoadedBundle bundle, OpAddMissionsPayload payload)
        {
            if (payload.MissionAssets == null || payload.MissionGroups == null || payload.MissionGroups.Length == 0)
                return;

            var missionAssets = payload.MissionAssets.Select(asset => ResourcesAssetResolver.ResolveBundleAsset(bundle, asset) as TextAsset).ToArray();
            if (missionAssets.Length == 0)
                return;

            var addedCount = 0;
            foreach (var groupName in payload.MissionGroups)
            {
                if (!resourceGroups.TryGetValue(groupName, out ResourceGroup groupAsset) || groupAsset == null)
                {
                    Plugin.Log.LogWarning($"[Ops] Mission group {groupName} not found");
                    continue;
                }

                foreach (var missionAsset in missionAssets)
                {
                    if (AddMission(groupAsset, missionAsset, groupName))
                        addedCount++;
                }
            }

            if (addedCount > 0)
                Plugin.Log.LogDebug($"[Ops] Added {addedCount} missions");
        }

        private static bool AddMission(MissionGroup.ResourceGroup groupAsset, TextAsset missionAsset, string groupName)
        {
            if (groupAsset == null || missionAsset == null || string.IsNullOrWhiteSpace(groupName))
                return false;

            TextAsset[] assets = groupAsset.assets;
            if (assets == null || assets.Length == 0)
                return false;

            MissionKey[] names = groupAsset.names;
            if (names == null || names.Length == 0)
                return false;

            MissionKey key = new(missionAsset.name, groupAsset);

            AssetsField.SetValue(groupAsset, assets.Append(missionAsset).ToArray());
            NamesField.SetValue(groupAsset, names.Append(key).ToArray());
            return true;
        }

        [HarmonyPatch(typeof(MissionGroup.ResourceGroup), MethodType.Constructor, [typeof(string), typeof(string)])]
        private static class ResourceGroupAddMissions
        {
            [HarmonyPrefix]
            private static void Prefix(MissionGroup.ResourceGroup __instance, string name)
            {
                if (name == "Free Flight")
                    resourceGroups.Add("FreeFlight", __instance);
                else
                    resourceGroups.Add(name, __instance);
            }
        }
    }
}