using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Blueprinter
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static string BundlesSignature = "NOBUNDLES";
        public static ManualLogSource Log => Instance?.Logger;
        public static Plugin Instance;
        public string GameVersion;
        private bool fastLoad;
        private bool skipAdditionalAssets;
        private BepInEx.Configuration.ConfigEntry<bool> bundleCacheEnabled;
        private BepInEx.Configuration.ConfigEntry<float> maxCacheSizeGiB;

        private void Awake()
        {
            Instance = this;
            GameVersion = Application.version;
            fastLoad = Config.Bind("General", "FastLoad", false).Value;
            skipAdditionalAssets = Config.Bind("General", "SkipAdditionalAssets", false).Value;
            bundleCacheEnabled = Config.Bind("Bundle Cache", "Enabled", true,
                "Cache loaded asset bundles into LZ4 format on disk for faster subsequent startups (original mods are not modified).");
            maxCacheSizeGiB = Config.Bind("Bundle Cache", "Max Size GiB", 10f,
                "Maximum bundle cache size in GiB, older/unused entries are removed first. (Set 0 for unlimited)");
            BundleCache.Configure(bundleCacheEnabled, maxCacheSizeGiB);
            GameObject mgr = Chainloader.ManagerObject;
            if (mgr != null)
            {
                mgr.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(mgr);
            }

            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll();
        }

        private async Task<GameObject> LoadAdditionalAssets()
        {
            GameObject rewired = null;
            try
            {
                while (NetworkManagerNuclearOption.i == null)
                    await Task.Delay(10);

                await ResourcesAsyncLoader.LoadPrefab("Rewired", destroyCancellationToken, go => rewired = go);
                if (rewired == null)
                {
                    Log.LogWarning("[Plugin] Could not load Rewired");
                    return null;
                }

                var missionKey = MissionGroup.Default.First();
                if (!missionKey.TryLoad(out var mission, out var error))
                {
                    Log.LogWarning($"[Plugin] Could not load mission {error}");
                    DestroyImmediate(rewired);
                    return null;
                }

                MissionManager.SetMission(mission, false);
                await NetworkManagerNuclearOption.i.StartHostAsync(new HostOptions(SocketType.Offline, GameState.SinglePlayer, mission.MapKey));
                return rewired;
            }
            catch (Exception ex)
            {
                if (rewired != null)
                    DestroyImmediate(rewired);

                Log.LogWarning("[Plugin] Additional asset setup failed");
                Log.LogWarning(ex);
                return null;
            }
        }

        private async Task<bool> StopAdditionalAssets(GameObject rewired)
        {
            try
            {
                await NetworkManagerNuclearOption.i.StopAsync(setDisconnectReason: true);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError("[Plugin] Could not stop host");
                Log.LogError(ex);
                return false;
            }
            finally
            {
                if (rewired != null)
                    DestroyImmediate(rewired);
            }
        }

        public IEnumerator RunRoutine(Encyclopedia encyclopedia)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            GameAssetNameIndex.Start();
            var bundleRegistry = new BundleRegistry();
            bool completed = false;
            var noYield = GameManager.IsHeadless;
            var loadingScreen = noYield ? null : BlueprinterLoadingScreen.Create(); 
            try
            {
                Action<LoadedBundle, int, int> reportProgress = (loadedBundle, current, total) => loadingScreen?.SetBundleProgress(loadedBundle, current, total);
                if (BundleCache.Enabled)
                {
                    if (loadingScreen != null)
                        loadingScreen.Status = "Preparing LZ4 asset bundle cache...";
                    
                    var cacheEnumerator = bundleRegistry.PrepareCacheCoroutine();
                    while (true)
                    {
                        bool moveNext;
                        try
                        {
                            moveNext = cacheEnumerator.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            Log.LogError("Cache preparation interrupted, continuing with available cached/original bundles: " + ex);
                            break;
                        }
                        
                        if (!moveNext)
                            break;
                        
                        yield return cacheEnumerator.Current;
                    }
                    if (loadingScreen != null && BundleCache.Warning != null)
                        loadingScreen.Warning = BundleCache.Warning;
                }

                if (fastLoad)
                {
                    try
                    {
                        bundleRegistry.FastLoad();
                    }
                    catch (Exception ex)
                    {
                        Log.LogError("[Plugin] Bundle loading failed");
                        Log.LogError(ex);
                        yield break;
                    }
                }
                else
                {
                    var scanEnum = bundleRegistry.ScanAndLoadCoroutine(status => { if (loadingScreen != null) loadingScreen.Status = status; }, reportProgress);
                    while (true)
                    {
                        bool moveNext;
                        try
                        {
                            moveNext = scanEnum.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            Log.LogError("[Plugin] Bundle loading failed");
                            Log.LogError(ex);
                            yield break;
                        }

                        if (!moveNext)
                            break;

                        if (!noYield)
                            yield return scanEnum.Current;
                    }
                }

                if (!bundleRegistry.ScanSucceeded)
                    yield break;

                BundleCache.RemoveRedundantCache(bundleRegistry.Bundles);

                BundlesSignature = bundleRegistry.Bundles.Count == 0 ? "NOBUNDLES" : string.Join("_", bundleRegistry.Bundles.Select(bundle => $"--{bundle.bundleName}-v{bundle.Manifest.modVersion}"));

                if (loadingScreen != null)
                {
                    loadingScreen.Status = "Preparing prefabs";
                    yield return null;
                }

                try
                {
                    PrefabHashAssigner.AssignFromBundles(bundleRegistry.Bundles);
                }
                catch (Exception ex)
                {
                    Log.LogError("[Plugin] Prefab preparation failed");
                    Log.LogError(ex);
                    yield break;
                }

                if (loadingScreen != null)
                {
                    loadingScreen.Status = "Applying patches";
                    yield return null;
                }

                var runner = new PatchRunner(bundleRegistry, reportProgress);
                IEnumerator patchEnum = runner.ApplyAllPatchesCoroutine();

                while (true)
                {
                    bool moveNext;
                    try
                    {
                        moveNext = patchEnum.MoveNext();
                    }
                    catch (Exception ex)
                    {
                        Log.LogError("[Plugin] Patching failed");
                        Log.LogError(ex);
                        yield break;
                    }

                    if (!moveNext)
                        break;
                    
                    // Don't yield with Fast Load on, the yield empty frames add up to be upwards of several seconds
                    if (!noYield && !fastLoad)
                        yield return patchEnum.Current;
                }

                if (runner.DeferredPatches.Count > 0 && !skipAdditionalAssets)
                {
                    Log.LogInfo("[Plugin] Loading additional assets");
                    if (loadingScreen != null)
                        loadingScreen.Status = "Loading additional assets";

                    var loadTask = LoadAdditionalAssets();
                    yield return new WaitUntil(() => loadTask.IsCompleted);

                    GameObject rewired = loadTask.Result;
                    if (loadingScreen != null)
                    {
                        loadingScreen.Status = "Applying patches";
                        yield return null;
                    }

                    Exception retryException = null;
                    GameAssetNameIndex.Clear();
                    var retryEnum = runner.RetryDeferredPatchesCoroutine();

                    while (true)
                    {
                        bool moveNext;
                        try
                        {
                            moveNext = retryEnum.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            retryException = ex;
                            break;
                        }

                        if (!moveNext)
                            break;

                        if (!noYield && !fastLoad)
                            yield return retryEnum.Current;
                    }

                    if (rewired != null)
                    {
                        if (SceneSingleton<GameplayUI>.i != null)
                            SceneSingleton<GameplayUI>.i.ResumeGame();

                        var stopTask = StopAdditionalAssets(rewired);
                        yield return new WaitUntil(() => stopTask.IsCompleted);

                        if (!stopTask.Result)
                            yield break;
                    }

                    if (retryException != null)
                    {
                        Log.LogError("[Plugin] Deferred patching failed");
                        Log.LogError(retryException);
                        yield break;
                    }

                    if (runner.DeferredPatches.Count > 0)
                    {
                        Log.LogWarning($"[Plugin] {runner.DeferredPatches.Count} deferred patches unresolved");
                        foreach (var item in runner.DeferredPatches)
                            Log.LogWarning($"[Plugin] {item.Bundle.Manifest.modName} patch {item.Patch?.GameAsset?.id ?? "unknown"} unresolved");
                    }
                }

                if (loadingScreen != null)
                {
                    loadingScreen.Status = "Applying ops";
                    yield return null;
                }

                try
                {
                    runner.ApplyAllOps(encyclopedia);
                    RegisterLiveries(bundleRegistry.Bundles);
                }
                catch (Exception ex)
                {
                    Log.LogError("[Plugin] Finalization failed");
                    Log.LogError(ex);
                    yield break;
                }

                if (loadingScreen != null)
                    yield return null;
                completed = true;
            }
            finally
            {
                // Clean up temporary unity references after load complete
                GameAssetNameIndex.End();
                // Keep loading screen present until all loading is complete, to prevent possible interaction going into
                // a match with BP not fully finished yet
                if (completed)
                    Log.LogInfo($"[Plugin] Done in {stopwatch.Elapsed.TotalSeconds:F2}s");
                else
                    Log.LogError($"[Plugin] Startup ended before completion after {stopwatch.Elapsed.TotalSeconds:F2}s");
                if (!completed && loadingScreen != null)
                {
                    loadingScreen.Status = "Initialisation failed, restart the game and check the log!";
                }
                else
                    BlueprinterLoadingScreen.DestroyInstance();
                
                if (completed && (bundleRegistry.DuplicateMods.Count > 0 || bundleRegistry.OutdatedMods.Count > 0) &&
                    !Application.isBatchMode && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    BlueprinterIssuePopup.Show(bundleRegistry.DuplicateMods, bundleRegistry.OutdatedMods);
                }
            }
        }

        private void RegisterLiveries(IReadOnlyList<LoadedBundle> bundles)
        {
            var locator = new LiveryAddressableLocator(bundles);
            if (locator.Count == 0)
            {
                Log.LogDebug("[Plugin] No livery overrides");
                return;
            }

            Addressables.ResourceManager.ResourceProviders.Add(new LiveryAddressableProvider());
            Addressables.AddResourceLocator(locator);
            Log.LogDebug($"[Plugin] Registered {locator.Count} liveries");
        }
    }
}