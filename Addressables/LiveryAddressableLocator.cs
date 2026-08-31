using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Blueprinter
{
    public class LiveryAddressableLocator : IResourceLocator
    {
        private readonly Dictionary<string, UnityEngine.Object> _liveries = new(StringComparer.OrdinalIgnoreCase);

        public LiveryAddressableLocator(IReadOnlyList<LoadedBundle> bundles)
        {
            foreach (var bundle in bundles)
            {
                if (bundle?.Manifest?.Addressables == null)
                    continue;

                foreach (var entry in bundle.Manifest.Addressables)
                {
                    if (entry?.BundleAsset == null || string.IsNullOrEmpty(entry.guid))
                        continue;

                    var asset = ResourcesAssetResolver.ResolveBundleAsset(bundle, entry.BundleAsset);
                    if (asset == null)
                        continue;

                    if (_liveries.ContainsKey(entry.guid))
                    {
                        Plugin.Log.LogWarning($"[LiveryAddressableLocator] Duplicate livery GUID {entry.guid} in {bundle.bundleName}");
                        continue;
                    }

                    _liveries.Add(entry.guid, asset);
                }
            }
        }

        public int Count => _liveries.Count;
        public string LocatorId => "Blueprinter.LiveryAddressableLocator";
        public IEnumerable<object> Keys => _liveries.Keys;

        public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
        {
            locations = null;

            var guid = GetGuid(key);
            if (string.IsNullOrEmpty(guid) || !_liveries.TryGetValue(guid, out var asset))
                return false;

            if (type != null && !type.IsInstanceOfType(asset))
                return false;

            locations = new IResourceLocation[] { new LiveryAddressableLocation(guid, asset) };
            return true;
        }

        private static string GetGuid(object key)
        {
            if (key is string guid)
                return guid;

            if (key is AssetReference reference)
                return reference.AssetGUID;

            return null;
        }
    }
}
