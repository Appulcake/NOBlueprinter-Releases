using System;
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

        public static void Handle(LoadedBundle bundle, OpAddLoadingScreensPayload payload)
        {
            Sprite[] newImages = payload?.imagesAssets?.Select(a => ResourcesAssetResolver.ResolveBundleAsset(bundle, a)).OfType<Sprite>().ToArray();

            if (newImages == null || newImages.Length == 0)
                return;

            LoadingScreen loadingScreen = Resources.FindObjectsOfTypeAll<LoadingScreen>().FirstOrDefault();
            if (loadingScreen == null)
            {
                Plugin.Log.LogWarning("[Ops] LoadingScreen prefab missing LoadingScreen component");
                return;
            }
            loadingScreen.images = [.. loadingScreen.images, .. newImages];

            Plugin.Log.LogDebug($"[Ops] Added {newImages.Length} images to loading screen pool");
        }
    }
}