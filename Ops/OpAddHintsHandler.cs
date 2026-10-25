using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Blueprinter.Ops
{
    [Serializable]
    public class OpAddHintsPayload
    {
        public HintEntry[] hints;

        [Serializable]
        public class HintEntry
        {
            public string type;
            public string text;
        }
    }

    public static class OpAddHintsHandler
    {
        public const string OpId = "OpAddHints";
        private const string DefaultHintType = "Did you know?";
        private static readonly List<OpAddHintsPayload.HintEntry> hints = [];
        private static readonly Dictionary<LoadedBundle, int> addedHintCounts = [];

        public static void Handle(LoadedBundle bundle, OpAddHintsPayload payload)
        {
            if (payload?.hints == null || payload.hints.Length == 0)
                return;

            int limit = Plugin.HintLimit;
            addedHintCounts.TryGetValue(bundle, out int addedCount);
            int remaining = limit - addedCount;
            int added = 0;
            int skipped = 0;

            foreach (var hint in payload.hints)
            {
                if (hint == null || string.IsNullOrWhiteSpace(hint.text))
                    continue;

                if (added >= remaining)
                {
                    skipped++;
                    continue;
                }

                string type = string.IsNullOrWhiteSpace(hint.type) ? DefaultHintType : hint.type;
                string modTitle = bundle.Manifest?.modName ?? bundle.bundleName;
                hints.Add(new OpAddHintsPayload.HintEntry
                {
                    type = $"{modTitle} - {type}",
                    text = hint.text
                });
                added++;
            }

            if (added > 0)
            {
                addedHintCounts[bundle] = addedCount + added;
                Plugin.Log.LogDebug($"[Ops] Registered {added} hints");
            }

            if (skipped > 0)
                Plugin.Log.LogWarning($"[Ops] ({bundle.bundleName}) Exceeded the hint limit ({limit}); skipped {skipped}");
        }

        [HarmonyPatch(typeof(HintsTipsDisplay), nameof(HintsTipsDisplay.ReadHints))]
        private static class ReadHintsPatch
        {
            [HarmonyPostfix]
            private static void Postfix(HintsTipsDisplay __instance)
            {
                foreach (var hint in hints)
                {
                    __instance.listHints.Add(new HintsTipsDisplay.HintTip
                    {
                        id = __instance.listHints.Count + 1,
                        type = hint.type,
                        text = hint.text
                    });
                }
            }
        }
    }
}
