#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using TMPro;

namespace TMPro.EditorUtilities
{
    [CustomEditor(typeof(TMP_DynamicText))]
    public class TMP_DynamicTextEditor : Editor
    {
        // ── Serialized properties ──────────────────────────────────
        SerializedProperty p_template;
        SerializedProperty p_everyFrame;
        SerializedProperty p_bindings;
        SerializedProperty p_formats;
        SerializedProperty p_dynamicBindings;

        // ── Autocomplete state ─────────────────────────────────────
        string       m_acInput      = string.Empty;
        List<string> m_suggestions  = new List<string>();
        bool         m_showSuggest  = false;
        int          m_suggestSel   = -1;
        Vector2      m_suggestScroll;
        static List<string> s_staticSuggestions; // "Application.version", etc.

        // ── Styles (lazy) ──────────────────────────────────────────
        GUIStyle sBox, sPreview, sSuggest, sSuggestHL, sChip, sChipBad, sHeader, sSectionBg;
        bool stylesBuilt;

        // ── Static type registry (mirror from runtime) ─────────────
        static readonly Dictionary<string, Type> k_StaticTypes =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "Application",    typeof(UnityEngine.Application)    },
                { "Time",           typeof(UnityEngine.Time)           },
                { "Screen",         typeof(UnityEngine.Screen)         },
                { "SystemInfo",     typeof(UnityEngine.SystemInfo)     },
                { "QualitySettings",typeof(UnityEngine.QualitySettings)},
                { "Physics",        typeof(UnityEngine.Physics)        },
                { "Input",          typeof(UnityEngine.Input)          },
                { "PlayerPrefs",    typeof(UnityEngine.PlayerPrefs)    },
                { "Display",        typeof(UnityEngine.Display)        },
                { "RenderSettings", typeof(UnityEngine.RenderSettings) },
            };

        static readonly Regex k_Rx = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);

        // ═══════════════════════════════════════════════════════════
        void OnEnable()
        {
            p_template   = serializedObject.FindProperty("m_templateText");
            p_everyFrame = serializedObject.FindProperty("m_updateEveryFrame");
            p_bindings   = serializedObject.FindProperty("m_bindings");
            p_formats    = serializedObject.FindProperty("m_formats");
            p_dynamicBindings = serializedObject.FindProperty("m_dynamicBindings");
            BuildStaticSuggestions();
        }

        // ═══════════════════════════════════════════════════════════
        public override void OnInspectorGUI()
        {
            EnsureStyles();
            serializedObject.Update();
            var comp = (TMP_DynamicText)target;

            // ── Header ───────────────────────────────────────────
            EditorGUILayout.Space(4);
            GUILayout.Label("TMP Dynamic Text Variables", sHeader);
            EditorGUILayout.Space(2);

            // ── Template ─────────────────────────────────────────
            Section("Template", () =>
            {
                EditorGUI.BeginChangeCheck();
                p_template.stringValue = EditorGUILayout.TextArea(
                    p_template.stringValue,
                    GUILayout.MinHeight(52));
                bool changed = EditorGUI.EndChangeCheck();

                DrawChips(p_template.stringValue, comp);

                DrawAutocomplete(comp);

                if (changed) { serializedObject.ApplyModifiedProperties(); comp.Refresh(); }
            });

            // ── Component Bindings ────────────────────────────────
            Section("Component Bindings", () =>
            {
                DrawBindingList(comp);
                if (GUILayout.Button("＋  Add Binding", GUILayout.Height(24)))
                {
                    p_bindings.arraySize++;
                    serializedObject.ApplyModifiedProperties();
                }
            });

            // ── Dynamic Text Bindings ─────────────────────────────
            Section("Dynamic Text Bindings", () =>
            {
                DrawDynamicBindingList(comp);
                if (GUILayout.Button("＋  Add Dynamic Text Binding", GUILayout.Height(24)))
                {
                    p_dynamicBindings.arraySize++;
                    serializedObject.ApplyModifiedProperties();
                }
                EditorGUILayout.HelpBox(
                    "Reference another TMP_DynamicText. Use {alias} in the template to embed its fully resolved text.",
                    MessageType.Info);
            });

            // ── Options ───────────────────────────────────────────
            Section("Options", () =>
            {
                EditorGUILayout.PropertyField(p_everyFrame,
                    new GUIContent("Update Every Frame",
                        "Re-evaluate template each frame. Good for timers, counters, etc."));
                EditorGUILayout.PropertyField(p_formats,
                    new GUIContent("Custom Formats",
                        "Override display format per placeholder (e.g. 'F2')."), true);
            });

            // ── Live Preview ──────────────────────────────────────
            DrawPreview(comp);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("⟳  Refresh Now", GUILayout.Height(26)))
                comp.Refresh();

            serializedObject.ApplyModifiedProperties();
        }

        // ═══════════════════════════════════════════════════════════
        //  Binding list
        // ═══════════════════════════════════════════════════════════

        void DrawBindingList(TMP_DynamicText comp)
        {
            int toDelete = -1;
            int count = p_bindings.arraySize;

            if (count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No bindings yet.\nAdd one to reference public fields/properties from any script.\n" +
                    "Usage in template:  {alias.fieldName}  or  {alias}",
                    MessageType.Info);
            }

            for (int i = 0; i < count; i++)
            {
                var elem = p_bindings.GetArrayElementAtIndex(i);
                var p_alias    = elem.FindPropertyRelative("alias");
                var p_go       = elem.FindPropertyRelative("targetGameObject");
                var p_compType = elem.FindPropertyRelative("componentTypeName");
                var p_member   = elem.FindPropertyRelative("memberName");

                // Read current binding for component / member dropdowns
                var goRef = p_go.objectReferenceValue as GameObject;

                // ── Draw box ───
                Rect boxRect = EditorGUILayout.BeginVertical(sBox);
                GUILayout.Space(4);

                // Row 1: alias  |  [×]
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Alias", GUILayout.Width(42));
                p_alias.stringValue = EditorGUILayout.TextField(p_alias.stringValue);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                    toDelete = i;
                EditorGUILayout.EndHorizontal();

                // Row 2: target GameObject
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(p_go, new GUIContent("GameObject"));
                bool goChanged = EditorGUI.EndChangeCheck();
                if (goChanged) { p_compType.stringValue = string.Empty; p_member.stringValue = string.Empty; }

                goRef = p_go.objectReferenceValue as GameObject;

                // Row 3: Component dropdown
                if (goRef != null)
                {
                    var compTypes = GetComponentTypeNames(goRef);
                    int compIdx   = Mathf.Max(0, compTypes.IndexOf(p_compType.stringValue));

                    EditorGUI.BeginChangeCheck();
                    compIdx = EditorGUILayout.Popup("Component",
                        compIdx < compTypes.Count ? compIdx : 0,
                        compTypes.ToArray());
                    if (EditorGUI.EndChangeCheck())
                    {
                        p_compType.stringValue = compIdx < compTypes.Count ? compTypes[compIdx] : string.Empty;
                        p_member.stringValue   = string.Empty;
                    }

                    // Row 4: Member dropdown
                    if (!string.IsNullOrEmpty(p_compType.stringValue))
                    {
                        var members = GetMemberNames(goRef, p_compType.stringValue, out var memberTypes);
                        if (members.Count > 0)
                        {
                            int mIdx = Mathf.Max(0, members.IndexOf(p_member.stringValue));
                            EditorGUI.BeginChangeCheck();
                            mIdx = EditorGUILayout.Popup("Member", mIdx, BuildMemberLabels(members, memberTypes));
                            if (EditorGUI.EndChangeCheck())
                                p_member.stringValue = mIdx < members.Count ? members[mIdx] : string.Empty;

                            // Snippet hint
                            if (!string.IsNullOrEmpty(p_alias.stringValue) && !string.IsNullOrEmpty(p_member.stringValue))
                            {
                                string snippet = "{" + p_alias.stringValue + "." + p_member.stringValue + "}";
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(new GUIContent("Snippet", "Click to append to template"), GUILayout.Width(54));
                                GUILayout.Label(snippet, sChip);
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("Insert →", EditorStyles.miniButton, GUILayout.Width(64)))
                                {
                                    p_template.stringValue += snippet;
                                    serializedObject.ApplyModifiedProperties();
                                    comp.Refresh();
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("No public readable members found.", MessageType.None);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Assign a GameObject to select a component and member.", MessageType.None);
                }

                GUILayout.Space(4);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }

            if (toDelete >= 0)
            {
                p_bindings.DeleteArrayElementAtIndex(toDelete);
                serializedObject.ApplyModifiedProperties();
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Dynamic Text Binding list
        // ═══════════════════════════════════════════════════════════

        void DrawDynamicBindingList(TMP_DynamicText comp)
        {
            int toDelete = -1;
            int count = p_dynamicBindings.arraySize;

            if (count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No dynamic bindings yet.\nReference another TMP_DynamicText component.\n" +
                    "Usage in template:  {alias}",
                    MessageType.Info);
            }

            for (int i = 0; i < count; i++)
            {
                var elem    = p_dynamicBindings.GetArrayElementAtIndex(i);
                var p_alias  = elem.FindPropertyRelative("alias");
                var p_source = elem.FindPropertyRelative("source");

                Rect boxRect = EditorGUILayout.BeginVertical(sBox);
                GUILayout.Space(4);

                // Row 1: alias | [×]
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Alias", GUILayout.Width(42));
                p_alias.stringValue = EditorGUILayout.TextField(p_alias.stringValue);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                    toDelete = i;
                EditorGUILayout.EndHorizontal();

                // Row 2: source TMP_DynamicText
                EditorGUILayout.PropertyField(p_source, new GUIContent("TMP_DynamicText Source",
                    "The TMP_DynamicText whose resolved text will be embedded."));

                // Guard against self-reference
                var srcRef = p_source.objectReferenceValue as TMP_DynamicText;
                if (srcRef != null && srcRef == comp)
                {
                    EditorGUILayout.HelpBox("⚠ Cannot reference itself — circular binding!", MessageType.Warning);
                    p_source.objectReferenceValue = null;
                }

                // Snippet hint
                if (!string.IsNullOrEmpty(p_alias.stringValue) && srcRef != null && srcRef != comp)
                {
                    string snippet = "{" + p_alias.stringValue + "}";
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent("Snippet", "Click to append to template"), GUILayout.Width(54));
                    GUILayout.Label(snippet, sChip);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Insert →", EditorStyles.miniButton, GUILayout.Width(64)))
                    {
                        p_template.stringValue += snippet;
                        serializedObject.ApplyModifiedProperties();
                        comp.Refresh();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.Space(4);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }

            if (toDelete >= 0)
            {
                p_dynamicBindings.DeleteArrayElementAtIndex(toDelete);
                serializedObject.ApplyModifiedProperties();
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Chips: detected {placeholders}
        // ═══════════════════════════════════════════════════════════

        void DrawChips(string template, TMP_DynamicText comp)
        {
            if (string.IsNullOrEmpty(template)) return;
            var matches = k_Rx.Matches(template);
            if (matches.Count == 0) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Found:", EditorStyles.miniLabel, GUILayout.Width(42));
            float x = 50f;
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                string ph    = m.Groups[1].Value;
                string label = "{" + ph + "}";
                bool   ok    = TryPreview(comp, ph, out _);
                var    style = ok ? sChip : sChipBad;
                float  w     = style.CalcSize(new GUIContent(label)).x + 8f;
                x += w + 4f;
                if (x > EditorGUIUtility.currentViewWidth - 20f)
                { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); GUILayout.Space(46); x = 46f + w + 4f; }
                GUILayout.Label(label, style);
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        // ═══════════════════════════════════════════════════════════
        //  Autocomplete for static suggestions
        // ═══════════════════════════════════════════════════════════

        void DrawAutocomplete(TMP_DynamicText comp)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Insert Variable", EditorStyles.miniBoldLabel, GUILayout.Width(100));

            EditorGUI.BeginChangeCheck();
            m_acInput = EditorGUILayout.TextField(m_acInput);
            if (EditorGUI.EndChangeCheck())
            {
                m_suggestSel = -1;
                if (m_acInput.Length >= 2)
                {
                    m_suggestions.Clear();
                    foreach (var s in s_staticSuggestions)
                        if (s.IndexOf(m_acInput, StringComparison.OrdinalIgnoreCase) >= 0)
                            m_suggestions.Add(s);
                    m_showSuggest = m_suggestions.Count > 0;
                }
                else { m_showSuggest = false; m_suggestions.Clear(); }
            }

            if (GUILayout.Button("Insert", GUILayout.Width(56)) && !string.IsNullOrEmpty(m_acInput))
            {
                p_template.stringValue += "{" + m_acInput.Trim('{', '}') + "}";
                m_acInput = string.Empty; m_showSuggest = false;
                serializedObject.ApplyModifiedProperties(); comp.Refresh();
            }
            EditorGUILayout.EndHorizontal();

            if (m_showSuggest)
            {
                float h = Mathf.Min(m_suggestions.Count * 20f + 6f, 120f);
                m_suggestScroll = EditorGUILayout.BeginScrollView(m_suggestScroll, GUILayout.Height(h));
                for (int i = 0; i < m_suggestions.Count; i++)
                {
                    if (GUI.Button(EditorGUILayout.GetControlRect(false, 20f),
                            m_suggestions[i], i == m_suggestSel ? sSuggestHL : sSuggest))
                    { m_acInput = m_suggestions[i]; m_suggestSel = i; m_showSuggest = false; }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Preview
        // ═══════════════════════════════════════════════════════════

        void DrawPreview(TMP_DynamicText comp)
        {
            string tmpl = p_template.stringValue;
            if (string.IsNullOrEmpty(tmpl)) return;

            string resolved = k_Rx.Replace(tmpl, m =>
            {
                string ph = m.Groups[1].Value.Trim();
                return TryPreview(comp, ph, out string v) ? v : ("??" + ph + "??");
            });

            Section("Live Preview", () =>
                EditorGUILayout.LabelField(resolved, sPreview));
        }

        // Preview resolver: calls into runtime ResolveOne via reflection to stay DRY
        bool TryPreview(TMP_DynamicText comp, string ph, out string val)
        {
            val = null;
            try
            {
                // Rebuild binding map first (private method, use reflection)
                var rebuildMI = typeof(TMP_DynamicText).GetMethod("RebuildBindingMap",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                rebuildMI?.Invoke(comp, null);

                var mi = typeof(TMP_DynamicText).GetMethod("ResolveOne",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (mi == null) { val = "?"; return false; }
                val = mi.Invoke(comp, new object[] { ph }) as string;
                return val != null;
            }
            catch { return false; }
        }

        // ═══════════════════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════════════════

        static List<string> GetComponentTypeNames(GameObject go)
        {
            var list = new List<string>();
            foreach (var c in go.GetComponents<Component>())
                if (c) list.Add(c.GetType().Name);
            return list;
        }

        static List<string> GetMemberNames(GameObject go, string typeName, out List<Type> types)
        {
            types = new List<Type>();
            var names = new List<string>();
            foreach (var c in go.GetComponents<Component>())
            {
                if (!c) continue;
                Type ct = c.GetType();
                bool match = false;
                for (Type t = ct; t != null && !match; t = t.BaseType)
                    if (t.Name == typeName || t.FullName == typeName) match = true;
                if (!match) continue;

                const BindingFlags f = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                foreach (var p in ct.GetProperties(f))
                    if (p.CanRead && p.GetIndexParameters().Length == 0)
                    { names.Add(p.Name); types.Add(p.PropertyType); }
                foreach (var fi in ct.GetFields(f))
                    { names.Add(fi.Name); types.Add(fi.FieldType); }
                break;
            }
            return names;
        }

        static string[] BuildMemberLabels(List<string> names, List<Type> types)
        {
            var labels = new string[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                string typeName = types[i].Name;
                labels[i] = names[i] + "  <" + typeName + ">";
            }
            return labels;
        }

        static void BuildStaticSuggestions()
        {
            if (s_staticSuggestions != null) return;
            s_staticSuggestions = new List<string>();
            const BindingFlags f = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            foreach (var kv in k_StaticTypes)
            {
                foreach (var p in kv.Value.GetProperties(f))
                    s_staticSuggestions.Add(kv.Key + "." + p.Name);
                foreach (var fi in kv.Value.GetFields(f))
                    s_staticSuggestions.Add(kv.Key + "." + fi.Name);
            }
            s_staticSuggestions.Sort(StringComparer.OrdinalIgnoreCase);
        }

        void Section(string title, Action draw)
        {
            EditorGUILayout.BeginVertical(sSectionBg);
            GUILayout.Label(title, EditorStyles.miniBoldLabel);
            draw();
            EditorGUILayout.EndVertical();
            GUILayout.Space(4);
        }

        // ═══════════════════════════════════════════════════════════
        //  Styles
        // ═══════════════════════════════════════════════════════════

        void EnsureStyles()
        {
            if (stylesBuilt) return;
            stylesBuilt = true;

            sBox = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8,8,4,4) };
            sSectionBg = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(6,6,4,6) };

            sPreview = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 12,
                padding  = new RectOffset(6,6,4,4)
            };
            sPreview.normal.textColor = new Color(0.25f, 0.85f, 0.35f);

            sSuggest = new GUIStyle(EditorStyles.label)
            { fontSize = 11, padding = new RectOffset(6,4,2,2) };

            sSuggestHL = new GUIStyle(sSuggest);
            sSuggestHL.normal.background = MakeTex(new Color(0.25f, 0.5f, 1f, 0.35f));
            sSuggestHL.normal.textColor  = Color.white;

            sChip = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize  = 10,
                padding   = new RectOffset(5,5,2,2),
                alignment = TextAnchor.MiddleCenter
            };
            sChip.normal.background = MakeTex(new Color(0.2f, 0.6f, 1f, 0.28f));
            sChip.normal.textColor  = new Color(0.5f, 0.9f, 1f);

            sChipBad = new GUIStyle(sChip);
            sChipBad.normal.background = MakeTex(new Color(1f, 0.35f, 0.25f, 0.28f));
            sChipBad.normal.textColor  = new Color(1f, 0.65f, 0.55f);

            sHeader = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        }

        static Texture2D MakeTex(Color col)
        {
            var tex = new Texture2D(2,2);
            tex.SetPixels(new[]{ col,col,col,col });
            tex.Apply();
            return tex;
        }
    }
}
#endif