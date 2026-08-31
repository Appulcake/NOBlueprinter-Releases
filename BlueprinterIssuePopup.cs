using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blueprinter
{
    public class BlueprinterIssuePopup : MonoBehaviour
    {
        private const float PanelWidth = 720f;
        private const float PanelHeight = 480f;
        private const float RowHeight = 26f;
        private const float ContentInset = 36f;

        private IReadOnlyList<string> duplicateMods;
        private IReadOnlyList<string> outdatedMods;
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle textStyle;
        private GUIStyle buttonStyle;
        private Rect panelRect;

        public static void Show(IReadOnlyList<string> duplicateMods, IReadOnlyList<string> outdatedMods)
        {
            var popup = new GameObject("BlueprinterIssuePopup").AddComponent<BlueprinterIssuePopup>();
            popup.duplicateMods = duplicateMods;
            popup.outdatedMods = outdatedMods;
        }

        private void OnGUI()
        {
            GUI.depth = -1000;

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                titleStyle.normal.textColor = new Color(1f, 0.4f, 0.32f);
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
                textStyle.normal.textColor = Color.white;
                sectionStyle = new GUIStyle(textStyle) { fontStyle = FontStyle.Bold };
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            }

            panelRect = new Rect((Screen.width - PanelWidth) * 0.5f, (Screen.height - PanelHeight) * 0.5f, PanelWidth, PanelHeight);

            var oldColor = GUI.color;
            GUI.color = new Color(0.12f, 0.12f, 0.14f, 0.98f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUI.Label(new Rect(panelRect.x + 28f, panelRect.y + 20f, panelRect.width - 56f, 34f), "Blueprinter issues detected", titleStyle);

            var y = panelRect.y + 82f;
            if (duplicateMods.Count > 0)
                y = DrawSection("Duplicate mod files", duplicateMods, y);

            if (outdatedMods.Count > 0)
                DrawSection("Outdated mods", outdatedMods, y);

            if (GUI.Button(new Rect(panelRect.xMax - 148f, panelRect.yMax - 64f, 120f, 40f), "Close", buttonStyle))
                Destroy(gameObject);
        }

        private float DrawSection(string title, IReadOnlyList<string> mods, float y)
        {
            var x = panelRect.x + ContentInset;
            var width = panelRect.width - ContentInset * 2f;
            GUI.Label(new Rect(x, y, width, RowHeight), title, sectionStyle);
            y += 34f;

            var visibleCount = Math.Min(3, mods.Count);
            for (var i = 0; i < visibleCount; i++)
            {
                GUI.Label(new Rect(x, y, width, RowHeight), mods[i], textStyle);
                y += RowHeight;
            }

            if (mods.Count > visibleCount)
            {
                GUI.Label(new Rect(x, y, width, RowHeight), $"and {mods.Count - visibleCount} more", textStyle);
                y += RowHeight;
            }

            return y + 28f;
        }

        private void OnDestroy()
        {
            BlueprinterLoadingScreen.DestroyInstance();
        }
    }
}
