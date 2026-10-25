using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Blueprinter.Ops
{
    [Serializable]
    public class OpAddLoadingScreensPayload
    {
        public AssetRef[] imagesAssets;
    }

    public static class OpAddLoadingScreensHandler
    {
        public const string OpId = "OpAddLoadingScreens";
        private static readonly Dictionary<LoadedBundle, int> addedImageCounts = [];

        public static void Handle(LoadedBundle bundle, OpAddLoadingScreensPayload payload)
        {
            int limit = Plugin.LoadingScreenImageLimit;
            addedImageCounts.TryGetValue(bundle, out int addedCount);
            int remaining = limit - addedCount;
            if (remaining <= 0)
            {
                if (payload?.imagesAssets?.Length > 0)
                    Plugin.Log.LogWarning($"[Ops] ({bundle.bundleName}) Reached the loading screen image limit ({limit})");
                return;
            }

            Sprite[] resolvedImages = payload?.imagesAssets?.Select(a => ResourcesAssetResolver.ResolveBundleAsset(bundle, a)).OfType<Sprite>().ToArray();
            if (resolvedImages == null || resolvedImages.Length == 0)
                return;

            Sprite[] newImages = resolvedImages.Take(remaining).ToArray();
            LoadingScreen loadingScreen = Resources.FindObjectsOfTypeAll<LoadingScreen>().FirstOrDefault();
            if (loadingScreen == null)
            {
                Plugin.Log.LogWarning($"[Ops] ({bundle.bundleName}) LoadingScreen prefab missing LoadingScreen component");
                return;
            }

            loadingScreen.images = [.. loadingScreen.images, .. newImages];
            addedImageCounts[bundle] = addedCount + newImages.Length;

            Plugin.Log.LogDebug($"[Ops] Added {newImages.Length} images to loading screen pool");
            if (resolvedImages.Length > newImages.Length)
                Plugin.Log.LogWarning($"[Ops] ({bundle.bundleName}) Exceeded the loading screen image limit ({limit}); skipped {resolvedImages.Length - newImages.Length}");
        }
    }
}