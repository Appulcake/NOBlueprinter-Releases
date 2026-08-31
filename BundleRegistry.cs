using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;

namespace Blueprinter
{
    public class LoadedBundle
    {
        public string bundleName;
        public string source;
        public AssetBundle AssetBundle;
        public PatchManifest Manifest;
    }

    public class BundleRegistry
    {
        private const string ManifestAssetName = "patch_manifest";
        public readonly List<LoadedBundle> Bundles = [];
        public readonly List<string> DuplicateMods = [];
        public readonly List<string> OutdatedMods = [];
        public bool ScanSucceeded;

        private class BundleCandidate
        {
            public string bundleName;
            public string source;
            public Assembly assembly;
            public string resourceName;
            public PatchManifest manifest;
        }


        public void FastLoad()
        {
            ScanSucceeded = false;
            AssetBundle bundle = null;
            try
            {
                foreach (var candidate in GetCandidates())
                {
                    bundle = LoadCandidate(candidate);
                    if (bundle == null)
                    {
                        Plugin.Log.LogError($"[BundleRegistry] Could not load {candidate.source}");
                        return;
                    }

                    var manifest = LoadManifest(bundle, candidate.source);
                    if (manifest == null)
                    {
                        Plugin.Log.LogError($"[BundleRegistry] Fast load failed {candidate.source}");
                        return;
                    }

                    var loadedBundle = new LoadedBundle
                    {
                        bundleName = bundle.name,
                        source = candidate.source,
                        AssetBundle = bundle,
                        Manifest = manifest
                    };

                    Bundles.Add(loadedBundle);
                    bundle = null;
                    Plugin.Log.LogInfo($"[BundleRegistry] Loaded {loadedBundle.bundleName} {manifest.modVersion} from {candidate.source}");
                }

                Bundles.Sort((a, b) => string.CompareOrdinal(a.bundleName, b.bundleName));
                CheckOutdatedMods();
                ScanSucceeded = true;
            }
            finally
            {
                bundle?.Unload(true);
                if (!ScanSucceeded)
                    UnloadBundles();
            }
        }

        public IEnumerator ScanAndLoadCoroutine(Action<string> reportStatus, Action<LoadedBundle, int, int> reportProgress)
        {
            ScanSucceeded = false;
            var candidates = new List<BundleCandidate>();
            foreach (var candidate in GetCandidates())
            {
                reportStatus?.Invoke($"Checking {(candidate.assembly == null ? Path.GetFileName(candidate.source) : candidate.resourceName)}");
                yield return null;

                if (TryInspectCandidate(candidate))
                    candidates.Add(candidate);
            }

            var selectedCandidates = new List<BundleCandidate>();
            foreach (var group in candidates.GroupBy(candidate => candidate.bundleName).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var groupCandidates = group.ToList();
                if (groupCandidates.Count == 1)
                {
                    selectedCandidates.Add(groupCandidates[0]);
                    continue;
                }

                Version highestVersion = null;
                BundleCandidate highestCandidate = null;
                var highestTied = false;
                foreach (var candidate in groupCandidates)
                {
                    if (!Version.TryParse(candidate.manifest.modVersion, out var version) || version.ToString() != candidate.manifest.modVersion)
                        continue;

                    var comparison = highestVersion == null ? 1 : version.CompareTo(highestVersion);
                    if (comparison > 0)
                    {
                        highestVersion = version;
                        highestCandidate = candidate;
                        highestTied = false;
                    }
                    else if (comparison == 0)
                    {
                        highestTied = true;
                    }
                }

                DuplicateMods.Add((highestCandidate ?? groupCandidates[0]).manifest.modName);

                if (highestCandidate == null || highestTied)
                {
                    if (highestCandidate == null)
                        Plugin.Log.LogWarning($"[BundleRegistry] Skipping duplicate {group.Key}, no valid version");
                    else
                        Plugin.Log.LogWarning($"[BundleRegistry] Skipping duplicate {group.Key} {highestVersion}");

                    foreach (var candidate in groupCandidates)
                        Plugin.Log.LogInfo($"[BundleRegistry] Candidate {candidate.manifest.modVersion} from {candidate.source}");

                    continue;
                }

                selectedCandidates.Add(highestCandidate);
                Plugin.Log.LogWarning($"[BundleRegistry] Using {highestCandidate.bundleName} {highestCandidate.manifest.modVersion} from {highestCandidate.source}");
                foreach (var candidate in groupCandidates)
                {
                    if (candidate != highestCandidate)
                        Plugin.Log.LogInfo($"[BundleRegistry] Skipping {candidate.bundleName} {candidate.manifest.modVersion} from {candidate.source}");
                }
            }

            reportStatus?.Invoke("Loading bundles");
            foreach (var candidate in selectedCandidates)
            {
                var loadedBundle = new LoadedBundle
                {
                    bundleName = candidate.bundleName,
                    source = candidate.source,
                    Manifest = candidate.manifest
                };

                int totalWork = PatchRunner.GetTotalWork(candidate.manifest);
                if (totalWork > 0)
                    reportProgress?.Invoke(loadedBundle, 0, totalWork);

                yield return null;

                AssetBundle bundle = null;
                try
                {
                    bundle = LoadCandidate(candidate);
                    if (bundle == null)
                    {
                        Plugin.Log.LogError($"[BundleRegistry] Could not load {candidate.source}");
                    }
                    else if (!string.Equals(bundle.name, candidate.bundleName, StringComparison.Ordinal))
                    {
                        Plugin.Log.LogError($"[BundleRegistry] Bundle name changed {candidate.bundleName} -> {bundle.name} in {candidate.source}");
                    }
                    else
                    {
                        loadedBundle.AssetBundle = bundle;
                        Bundles.Add(loadedBundle);
                        bundle = null;
                        Plugin.Log.LogInfo($"[BundleRegistry] Loaded {candidate.bundleName} {candidate.manifest.modVersion} from {candidate.source}");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[BundleRegistry] Load failed {candidate.source}");
                    Plugin.Log.LogError(ex);
                }

                bundle?.Unload(true);
                UnloadBundles();
                yield break;
            }

            CheckOutdatedMods();
            ScanSucceeded = true;
        }

        private static bool TryInspectCandidate(BundleCandidate candidate)
        {
            AssetBundle bundle = null;
            try
            {
                bundle = LoadCandidate(candidate);
                if (bundle == null)
                {
                    Plugin.Log.LogWarning($"[BundleRegistry] Could not load {candidate.source}");
                    return false;
                }

                var manifest = LoadManifest(bundle, candidate.source);
                if (manifest == null)
                    return false;

                candidate.bundleName = bundle.name;
                candidate.manifest = manifest;
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[BundleRegistry] Inspect failed {candidate.source}");
                Plugin.Log.LogWarning(ex);
                return false;
            }
            finally
            {
                bundle?.Unload(true);
            }
        }

        private static IEnumerable<BundleCandidate> GetCandidates()
        {
            foreach (var path in Directory.EnumerateFiles(Paths.PluginPath, "*.nobp", SearchOption.AllDirectories))
                yield return new BundleCandidate { source = path };

            foreach (var candidate in GetEmbeddedCandidates())
                yield return candidate;
        }

        private static PatchManifest LoadManifest(AssetBundle bundle, string source)
        {
            var manifestAsset = bundle.LoadAsset<TextAsset>(ManifestAssetName);
            if (manifestAsset == null)
            {
                Plugin.Log.LogWarning($"[BundleRegistry] Missing {ManifestAssetName} in {source}");
                return null;
            }

            var manifest = JsonUtilities.Deserialize<PatchManifest>(manifestAsset.text);
            if (manifest == null || string.IsNullOrEmpty(manifest.modName))
            {
                Plugin.Log.LogWarning($"[BundleRegistry] Invalid manifest in {source}");
                return null;
            }

            if (manifest.schemaVersion > 3)
            {
                Plugin.Log.LogWarning($"[BundleRegistry] Unsupported manifest schema {manifest.schemaVersion} in {source}; runtime supports up to 3");
                return null;
            }

            if (string.IsNullOrEmpty(manifest.gameVersion))
                manifest.gameVersion = "0.34.2";

            return manifest;
        }

        private void CheckOutdatedMods()
        {
            foreach (var bundle in Bundles)
            {
                if (!Version.TryParse(bundle.Manifest.gameVersion, out var modGameVersion) || !Version.TryParse(Plugin.Instance.GameVersion, out var gameVersion) || modGameVersion.Major != gameVersion.Major || modGameVersion.Minor != gameVersion.Minor)
                {
                    OutdatedMods.Add(string.IsNullOrEmpty(bundle.Manifest.modVersion) ? bundle.Manifest.modName : $"{bundle.Manifest.modName}  {bundle.Manifest.modVersion}");
                    Plugin.Log.LogWarning($"[BundleRegistry] {bundle.Manifest.modName} {bundle.Manifest.modVersion} targets game {bundle.Manifest.gameVersion}, current {Plugin.Instance.GameVersion}");
                }
            }
        }

        private void UnloadBundles()
        {
            foreach (var loadedBundle in Bundles)
                loadedBundle.AssetBundle.Unload(true);

            Bundles.Clear();
        }

        private static AssetBundle LoadCandidate(BundleCandidate candidate)
        {
            AssetBundle bundle;
            if (candidate.assembly == null)
            {
                bundle = AssetBundle.LoadFromFile(candidate.source);
            }
            else
            {
                using var stream = candidate.assembly.GetManifestResourceStream(candidate.resourceName);
                if (stream == null)
                {
                    Plugin.Log.LogWarning($"[BundleRegistry] Missing resource {candidate.source}");
                    return null;
                }

                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                bundle = AssetBundle.LoadFromMemory(memory.ToArray());
            }

            return bundle;
        }

        private static IEnumerable<BundleCandidate> GetEmbeddedCandidates()
        {
            HashSet<Assembly> seen = [];
            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                var assembly = plugin?.Instance?.GetType().Assembly;
                if (assembly == null || !seen.Add(assembly))
                    continue;

                string[] resources;
                try
                {
                    resources = assembly.GetManifestResourceNames();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[BundleRegistry] Resource scan failed {assembly.GetName().Name}");
                    Plugin.Log.LogWarning(ex);
                    continue;
                }

                var pluginName = assembly.GetName().Name;
                foreach (var resource in resources)
                {
                    if (!resource.EndsWith(".nobp", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var candidate = new BundleCandidate
                    {
                        source = $"resource:{pluginName}:{resource}",
                        assembly = assembly,
                        resourceName = resource
                    };

                    yield return candidate;
                }
            }
        }
    }
}
