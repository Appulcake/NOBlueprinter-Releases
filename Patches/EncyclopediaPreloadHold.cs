using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarmonyLib;

namespace Blueprinter;

// MainMenu.StartAsync() waits on Encyclopedia.Preload() to finish before initialising steam server
// A startup with BP and cold cache can turn long enough where it finishes after this, so server starts with
// NOBUNDLES version
// Artificially extend what game sees as Encyclopedia.Preload() on dedicated servers until BP is finished loading,
// so that server only sets up and starts advertising itself once BP is finished and version is set
[HarmonyPatch(typeof(Encyclopedia), nameof(Encyclopedia.Preload), typeof(CancellationToken))]
internal static class EncyclopediaPreloadHold
{
    [HarmonyPostfix]
    private static void Postfix(ref UniTask __result, CancellationToken __0)
    {
        if (!GameManager.IsHeadless)
            return;
        
        var original = __result;
        __result = WaitForBlueprinter(original, __0);
    }
    
    private static async UniTask WaitForBlueprinter(UniTask original, CancellationToken cancellationToken)
    {
        await original;
        if (!Plugin.StartupStarted)
            throw new InvalidOperationException("Blueprinter did not start during Encyclopedia.Preload(), " +
                                                "refusing to initialise the dedicated server.");
        
        if (!Plugin.StartupFinished)
        {
            Plugin.Log.LogInfo("[Plugin] Waiting for Blueprinter before dedicated server network initialisation");
            await UniTask.WaitUntil(() => Plugin.StartupFinished, cancellationToken: cancellationToken);
        }
        
        if (!Plugin.StartupSucceeded)
            throw new InvalidOperationException("Blueprinter initialisation failed, refusing to initialise the " +
                                                "dedicated server with incomplete mods.");
        
        Plugin.Log.LogInfo("[Plugin] Dedicated server preload done, continuing setting up server");
    }
}