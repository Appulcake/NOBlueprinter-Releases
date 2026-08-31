using System;
using System.Collections.Generic;
using System.Linq;
using Mirage;
using UnityEngine;

namespace Blueprinter
{
    public static class PrefabHashAssigner
    {
        public static void AssignFromBundles(IReadOnlyList<LoadedBundle> bundles)
        {
            if (bundles == null || bundles.Count == 0)
                return;

            var taken = new HashSet<int>();
            foreach (var ni in Resources.FindObjectsOfTypeAll<NetworkIdentity>())
            {
                if (ni && ni.PrefabHash != 0)
                    taken.Add(ni.PrefabHash);
            }

            var candidates = new List<(NetworkIdentity ni, string key)>();

            foreach (var bundle in bundles)
            {
                if (bundle?.AssetBundle == null)
                    continue;

                GameObject[] assets;
                try
                {
                    assets = bundle.AssetBundle.LoadAllAssets<GameObject>();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[PrefabHashAssigner] Could not load GameObjects from {bundle.source}");
                    Plugin.Log.LogWarning(ex);
                    continue;
                }

                foreach (var go in assets ?? Array.Empty<GameObject>())
                {
                    if (!go)
                        continue;

                    foreach (var ni in go.GetComponentsInChildren<NetworkIdentity>(true))
                    {
                        if (!ni)
                            continue;

                        candidates.Add((ni, BuildStableKey(bundle.bundleName, go, ni)));
                    }
                }
            }

            if (candidates.Count == 0)
                return;

            candidates = candidates.OrderBy(c => c.key, StringComparer.Ordinal).ThenBy(c => c.ni.transform.GetSiblingIndex()).ToList();

            var assigned = new HashSet<int>(taken);
            int reused = 0, newlyAssigned = 0;
            int next = 1;

            foreach (var (ni, _) in candidates)
            {
                int current = ni.PrefabHash;
                if (current != 0 && assigned.Add(current))
                {
                    reused++;
                    continue;
                }

                while (assigned.Contains(next))
                    next++;

                ni.PrefabHash = next;
                assigned.Add(next);
                newlyAssigned++;
                next++;
            }

            Plugin.Log.LogDebug($"[PrefabHashAssigner] {candidates.Count} prefabs, {reused} reused, {newlyAssigned} assigned, {taken.Count} taken");
        }

        private static string BuildStableKey(string bundleName, GameObject root, NetworkIdentity ni)
        {
            var keyPrefix = string.IsNullOrEmpty(bundleName) ? "<bundle>" : bundleName;
            var rootName = root ? root.name ?? "<root>" : "<root>";

            var stack = new Stack<string>();
            var t = ni ? ni.transform : null;
            var rootTransform = root ? root.transform : null;

            while (t != null)
            {
                stack.Push(t.name);
                if (rootTransform != null && t == rootTransform)
                    break;
                t = t.parent;
            }

            var path = string.Join("/", stack);
            return $"{keyPrefix}|{rootName}|{path}";
        }
    }
}
