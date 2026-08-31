using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Blueprinter
{
    public class BlueprinterLoadingScreen : MonoBehaviour
    {
        private const float Left = 120f;
        private const float HeaderTop = 60f;
        private const float RowsTop = 110f;
        private const float NameWidth = 200f;
        private const float Gap = 10f;
        private const float BarWidth = 420f;
        private const float CountWidth = 120f;
        private const float RowHeight = 36f;
        private const float RowSpacing = 10f;
        private const float BarHeight = 18f;
        private const float BaselineHeight = 2f;

        private static readonly Color BackgroundColor = new Color(0.015f, 0.04f, 0.02f, 0.95f);
        private static readonly Color HeaderColor = new Color(0.6f, 1f, 0.6f, 1f);
        private static readonly Color BodyColor = new Color(0.5f, 1f, 0.5f, 1f);

        private class BundleProgress
        {
            public LoadedBundle Bundle;
            public string Name;
            public int Current;
            public int Total;
        }

        private static BlueprinterLoadingScreen Instance;

        private readonly List<BundleProgress> bundles = [];
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;
        private GUIStyle countStyle;
        public string Status;

        public static BlueprinterLoadingScreen Create()
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return null;

            if (Instance != null)
                return Instance;

            var go = new GameObject("BlueprinterLoadingScreen");
            DontDestroyOnLoad(go);

            Instance = go.AddComponent<BlueprinterLoadingScreen>();
            Instance.CreateClickBlocker();
            return Instance;
        }

        public static void DestroyInstance()
        {
            if (Instance == null)
                return;

            Destroy(Instance.gameObject);
            Instance = null;
        }

        public void SetBundleProgress(LoadedBundle bundle, int current, int total)
        {
            var progress = bundles.Find(item => item.Bundle == bundle);
            if (progress == null)
            {
                progress = new BundleProgress
                {
                    Bundle = bundle,
                    Name = bundle.Manifest.modName,
                    Total = total
                };
                bundles.Add(progress);
            }

            progress.Current = current;
        }

        private void CreateClickBlocker()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;

            gameObject.AddComponent<GraphicRaycaster>();

            var blocker = new GameObject("ClickBlocker", typeof(RectTransform), typeof(Image));
            blocker.transform.SetParent(transform, false);

            var image = blocker.GetComponent<Image>();
            image.color = Color.clear;

            var rect = blocker.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUI.depth = 1000;
            var oldColor = GUI.color;
            GUI.color = BackgroundColor;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            var title = $"BLUEPRINTER {MyPluginInfo.PLUGIN_VERSION}";
            if (!string.IsNullOrEmpty(Status))
                title += $"    {Status}";

            GUI.Label(new Rect(Left, HeaderTop, NameWidth + Gap + BarWidth + Gap + CountWidth, 40f), title, headerStyle);

            for (int row = 0; row < bundles.Count; row++)
            {
                var progress = bundles[row];
                float y = RowsTop + row * (RowHeight + RowSpacing);
                GUI.Label(new Rect(Left, y, NameWidth, RowHeight), progress.Name, bodyStyle);

                float barX = Left + NameWidth + Gap;
                float barY = y + (RowHeight - BarHeight) * 0.5f;
                GUI.color = BodyColor;
                GUI.DrawTexture(new Rect(barX, barY + BarHeight - BaselineHeight, BarWidth, BaselineHeight), Texture2D.whiteTexture);

                if (progress.Current > 0)
                {
                    float fillWidth = BarWidth * progress.Current / progress.Total;
                    GUI.DrawTexture(new Rect(barX, barY, fillWidth, BarHeight), Texture2D.whiteTexture);
                }

                GUI.color = oldColor;
                GUI.Label(new Rect(barX + BarWidth + Gap, y, CountWidth, RowHeight), $"{progress.Current}/{progress.Total}", countStyle);
            }
        }

        private void EnsureStyles()
        {
            if (headerStyle != null)
                return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            headerStyle.normal.textColor = HeaderColor;

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            bodyStyle.normal.textColor = BodyColor;

            countStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleRight
            };
        }
    }
}
