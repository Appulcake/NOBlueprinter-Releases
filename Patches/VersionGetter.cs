using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blueprinter
{
    public static class VersionGetterPatch
    {
        //TODO please stop this madness, use something sensible like steam lobby data
        private static string versionString = "";
        // isolate from vanilla
        [HarmonyPatch(typeof(Application), nameof(Application.version), MethodType.Getter)]
        private static class ApplicationVersionPatch
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ref string __result)
            {
                __result += $"_{MyPluginInfo.PLUGIN_GUID}-v{MyPluginInfo.PLUGIN_VERSION}_{Plugin.BundlesSignature}";

                versionString = $"Nuclear Option-v{__result.Replace("_", "\n").Replace("--", "    ")}";

                if (__result.Length > 100)
                {
                    Plugin.Log.LogDebug($"Version string too long ({__result})({__result.Length} chars), hashing");
                    var split = __result.IndexOf('_');
                    var prefix = split >= 0 ? __result[..split] : __result;

                    using var sha = System.Security.Cryptography.SHA256.Create();
                    var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(__result));
                    var hash = System.BitConverter.ToString(bytes, 0, 6).Replace("-", "").ToLowerInvariant();

                    __result = prefix + "_" + hash;
                }

                Plugin.Log.LogInfo($"Updated game version to {__result}");
                if (SceneManager.GetActiveScene().name is "MultiplayerMenu")
                    VersionDisplayOverlay.SetText(versionString);
            }
        }

        [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.Start))] 
        private static class LeaderboardVersionPatch
        {
            private static void Postfix(SettingsMenu __instance)
            {
                VersionDisplayOverlay.SetText(versionString, __instance.transform);
            }
        }

        private class VersionDisplayOverlay : MonoBehaviour
        {
            private const string ObjectName = "__PatchedVersionDisplay";
            private static string _text = "";

            public static void SetText(string text, Transform parent = null)
            {
                _text = text;

                GameObject target;

                if (parent == null)
                {
                    target = GameObject.Find(ObjectName);
                    if (target == null)
                        target = new GameObject(ObjectName);
                }
                else
                    target = parent.gameObject;

                if (!target.GetComponent<VersionDisplayOverlay>())
                    target.AddComponent<VersionDisplayOverlay>();
            }

            private void OnGUI()
            {
                if (string.IsNullOrEmpty(_text))
                    return;

                GUI.Label(new Rect(10, 10, Screen.width - 20, Screen.height - 20), _text);
            }
        }
    }
}