#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace TMPro.EditorUtilities
{
    /// <summary>
    /// Adds "GameObject → UI → TMP Dynamic Text" to the Unity hierarchy
    /// context menu and the top GameObject menu.
    ///
    /// Creates a fully configured:
    ///   • RectTransform
    ///   • TextMeshProUGUI
    ///   • TMP_DynamicText
    ///
    /// If no Canvas exists in the scene, one is created automatically
    /// together with an EventSystem.
    /// </summary>
    public static class TMP_DynamicTextCreator
    {
        private const string k_Menu = "GameObject/UI/Dynamic Text - TextMeshPro";
        private const int k_Priority = 2001;

        [MenuItem(k_Menu, false, k_Priority)]
        private static void Create(MenuCommand cmd)
        {
            // ── Resolve parent ─────────────────────────────────────
            GameObject parent = cmd.context as GameObject;

            if (parent != null)
            {
                bool insideCanvas = parent.GetComponent<Canvas>() != null
                                 || parent.GetComponentInParent<Canvas>() != null;
                if (!insideCanvas) parent = null;
            }

            if (parent == null) parent = GetOrCreateCanvas();

            // ── Create GameObject with RectTransform from the start ─
            // Passing typeof(RectTransform) to the constructor makes Unity
            // create RectTransform instead of the default Transform.
            var go = new GameObject("Dynamic Text (TMP)", typeof(RectTransform));
            GameObjectUtility.SetParentAndAlign(go, parent);

            // Now RectTransform already exists — just configure it
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300f, 60f);
            rt.anchoredPosition = Vector2.zero;

            // ── TextMeshProUGUI ────────────────────────────────────
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 28f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = "{Application.version}";

            // ── TMP_DynamicText ────────────────────────────────────
            var dyn = go.AddComponent<TMP_DynamicText>();
            var so = new SerializedObject(dyn);
            so.FindProperty("m_templateText").stringValue = "{Application.version}";
            so.ApplyModifiedProperties();

            // ── Undo + select ──────────────────────────────────────
            Undo.RegisterCreatedObjectUndo(go, "Create Dynamic Text");
            Selection.activeGameObject = go;

            EditorApplication.delayCall += () =>
            {
                if (dyn != null) dyn.Refresh();
            };
        }

        [MenuItem(k_Menu, true)]
        private static bool Validate() => true;

        // ── Canvas / EventSystem helpers ───────────────────────────
        private static GameObject GetOrCreateCanvas()
        {
            var existing = Object.FindObjectOfType<Canvas>();
            if (existing != null) return existing.gameObject;

            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");

            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
            }

            return canvasGO;
        }
    }
}
#endif