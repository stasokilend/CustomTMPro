using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TMPro
{
    /// <summary>
    /// Attach this component alongside TextMeshProUGUI to enable dynamic variable binding.
    ///
    /// TEMPLATE SYNTAX:
    ///   {alias}               — value of a ComponentBinding (uses its default memberName)
    ///   {alias.memberName}    — specific field/property from a named ComponentBinding
    ///   {ComponentType.field} — auto-finds component on THIS GameObject
    ///   {Application.version} — static Unity engine property
    ///   {Time.time:F2}        — with optional C# format specifier after ':'
    ///
    /// RESOLUTION PRIORITY:
    ///   1. Code-registered delegates  (RegisterVariable)
    ///   2. Named ComponentBindings    ({alias} / {alias.member})
    ///   3. Components on same GO      ({ComponentTypeName.member})
    ///   4. Static Unity engine types  ({Application.version})
    /// </summary>
    [AddComponentMenu("UI/TMP Dynamic Text Variables")]
    [RequireComponent(typeof(TextMeshProUGUI))]
    [ExecuteAlways]
    public class TMP_DynamicText : MonoBehaviour
    {
        // ───────────────────────────────────────────────────────────
        //  Serialized fields
        // ───────────────────────────────────────────────────────────

        [Tooltip("Template text. E.g. 'HP: {player.health}  |  v{Application.version}'")]
        [SerializeField] private string m_templateText = string.Empty;

        [Tooltip("Re-evaluate the template every frame. Enable for counters, timers, etc.")]
        [SerializeField] private bool m_updateEveryFrame = false;

        [Tooltip("Drag any GameObject here and bind one of its component's fields/properties.")]
        [SerializeField] private List<ComponentBinding> m_bindings = new List<ComponentBinding>();

        [Tooltip("Optional: override C# format per placeholder. E.g. placeholder='Time.time', format='F1'")]
        [SerializeField] private List<PlaceholderFormat> m_formats = new List<PlaceholderFormat>();

        [Tooltip("Bind another TMP_DynamicText component. Use {alias} in template to embed its resolved text.")]
        [SerializeField] private List<DynamicTextBinding> m_dynamicBindings = new List<DynamicTextBinding>();

        // ───────────────────────────────────────────────────────────
        //  Runtime state
        // ───────────────────────────────────────────────────────────

        private TextMeshProUGUI m_textComponent;
        private readonly Dictionary<string, ComponentBinding> m_bindingMap =
            new Dictionary<string, ComponentBinding>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DynamicTextBinding> m_dynamicBindingMap =
            new Dictionary<string, DynamicTextBinding>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MemberInfo> m_memberCache =
            new Dictionary<string, MemberInfo>();

        private static readonly Dictionary<string, Func<string>> s_customVars =
            new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Type> s_staticTypes = BuildStaticTypes();
        private static readonly Regex k_Regex = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);
        private static readonly Dictionary<string, Func<string, string>> s_namespaceResolvers =
            new Dictionary<string, Func<string, string>>(StringComparer.OrdinalIgnoreCase);

        // ───────────────────────────────────────────────────────────
        //  Public API
        // ───────────────────────────────────────────────────────────

        public string templateText
        {
            get => m_templateText;
            set { m_templateText = value; Refresh(); }
        }

        /// <summary>Register a code-side variable accessible as {key} in any template.</summary>
        public static void RegisterVariable(string key, Func<string> provider)
            => s_customVars[key] = provider;

        public static void UnregisterVariable(string key) => s_customVars.Remove(key);

        /// <summary>Force immediate re-evaluation.</summary>
        public void Refresh()
        {
            if (!m_textComponent) m_textComponent = GetComponent<TextMeshProUGUI>();
            if (!m_textComponent || string.IsNullOrEmpty(m_templateText)) return;
            RebuildBindingMap();
            m_textComponent.text = k_Regex.Replace(m_templateText,
                m => ResolveOne(m.Groups[1].Value.Trim()) ?? m.Value);
        }

        public static void RegisterNamespaceResolver(string ns, Func<string, string> resolver)
        => s_namespaceResolvers[ns] = resolver;

    public static void UnregisterNamespaceResolver(string ns)
        => s_namespaceResolvers.Remove(ns);

        // ───────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ───────────────────────────────────────────────────────────

        private void Awake()   => m_textComponent = GetComponent<TextMeshProUGUI>();
        private void OnEnable() { m_textComponent = GetComponent<TextMeshProUGUI>(); Refresh(); }
        private void Update()  { if (m_updateEveryFrame) Refresh(); }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () => { if (this) Refresh(); };
        }
#endif

        // ───────────────────────────────────────────────────────────
        //  Internal resolution  (internal so Editor can preview)
        // ───────────────────────────────────────────────────────────

        private void RebuildBindingMap()
        {
            m_bindingMap.Clear();
            if (m_bindings == null) return;
            foreach (var b in m_bindings)
                if (b != null && !string.IsNullOrEmpty(b.alias))
                    m_bindingMap[b.alias] = b;

            m_dynamicBindingMap.Clear();
            if (m_dynamicBindings == null) return;
            foreach (var d in m_dynamicBindings)
                if (d != null && !string.IsNullOrEmpty(d.alias))
                    m_dynamicBindingMap[d.alias] = d;
        }

        internal string ResolveOne(string raw)
        {
            // Split optional format suffix  e.g.  "Time.time:F2"
            string key = raw, format = GetInspectorFormat(raw);
            int ci = raw.IndexOf(':');
            if (ci > 0) { key = raw.Substring(0, ci); format = raw.Substring(ci + 1); }

            // 1. Code-registered delegate
            if (s_customVars.TryGetValue(key, out var fn)) return Format(fn?.Invoke(), format);

            // 1.5. Dynamic TMP_DynamicText binding
            if (m_dynamicBindingMap.TryGetValue(key, out var dynBinding) && dynBinding.IsValid())
                return Format(dynBinding.GetResolvedText(), format);

            int dot = key.IndexOf('.');
            string head = dot > 0 ? key.Substring(0, dot) : key;
            string tail = dot > 0 ? key.Substring(dot + 1) : null;

            // 2. Named ComponentBinding
            if (m_bindingMap.TryGetValue(head, out var binding) && binding.IsValid())
            {
                string member = tail ?? binding.memberName;
                return Format(GetMemberValue(binding.GetComponent(), member, false), format);
            }

            if (dot <= 0) return null;

            // 3. Component on same GameObject
            Component selfComp = FindComponentByName(head);
            if (selfComp != null)
                return Format(GetMemberValue(selfComp, tail, false), format);

            // 4. Static Unity engine type
            Type staticType = ResolveStaticType(head);
            if (staticType != null)
                return Format(GetMemberValue(staticType, tail, true), format);

            return null;
        }

        // ───────────────────────────────────────────────────────────
        //  Reflection helpers
        // ───────────────────────────────────────────────────────────

        private object GetMemberValue(object target, string member, bool isStatic)
        {
            if (target == null || string.IsNullOrEmpty(member)) return null;
            Type type = isStatic ? (Type)target : target.GetType();
            string cKey = type.FullName + "." + member + (isStatic ? "_s" : "_i");

            if (!m_memberCache.TryGetValue(cKey, out MemberInfo mi))
            {
                var flags = isStatic
                    ? BindingFlags.Public | BindingFlags.Static  | BindingFlags.FlattenHierarchy
                    : BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                mi = (MemberInfo)type.GetProperty(member, flags) ?? type.GetField(member, flags);
                m_memberCache[cKey] = mi;
            }
            if (mi == null) return null;
            try
            {
                object inst = isStatic ? null : target;
                return mi is PropertyInfo p ? p.GetValue(inst) :
                       mi is FieldInfo    f ? f.GetValue(inst) : null;
            }
            catch { return null; }
        }

        private Component FindComponentByName(string typeName)
        {
            foreach (var c in GetComponents<Component>())
            {
                if (!c) continue;
                for (Type t = c.GetType(); t != null && t != typeof(UnityEngine.Object); t = t.BaseType)
                    if (string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase))
                        return c;
            }
            return null;
        }

        private static string Format(object val, string fmt)
        {
            if (val == null) return string.Empty;
            return (!string.IsNullOrEmpty(fmt) && val is IFormattable f) ? f.ToString(fmt, null) : val.ToString();
        }

        private string GetInspectorFormat(string ph)
        {
            if (m_formats == null) return null;
            foreach (var pf in m_formats) if (pf.placeholder == ph) return pf.format;
            return null;
        }

        private static Type ResolveStaticType(string name)
        {
            if (s_staticTypes.TryGetValue(name, out var t)) return t;
            return Type.GetType("UnityEngine." + name + ", UnityEngine.CoreModule")
                ?? Type.GetType("UnityEngine." + name + ", UnityEngine")
                ?? Type.GetType(name);
        }

        private static Dictionary<string, Type> BuildStaticTypes() =>
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

        // ───────────────────────────────────────────────────────────
        //  Serializable nested types
        // ───────────────────────────────────────────────────────────

        [Serializable]
        public class ComponentBinding
        {
            [Tooltip("Short name used in template: 'player' → {player.health}")]
            public string alias = string.Empty;

            [Tooltip("Source GameObject (can be any object in the scene).")]
            public GameObject targetGameObject;

            [Tooltip("Type name of the target component (filled by custom editor).")]
            public string componentTypeName = string.Empty;

            [Tooltip("Default member when placeholder is just {alias} without a dot.")]
            public string memberName = string.Empty;

            // ── Runtime cache ──
            private Component m_cached;
            private string    m_cachedTypeName;

            public bool IsValid() =>
                targetGameObject != null && !string.IsNullOrEmpty(componentTypeName);

            public Component GetComponent()
            {
                if (!targetGameObject) return null;
                if (m_cached && m_cachedTypeName == componentTypeName) return m_cached;
                foreach (var c in targetGameObject.GetComponents<Component>())
                {
                    if (!c) continue;
                    for (Type t = c.GetType(); t != null && t != typeof(UnityEngine.Object); t = t.BaseType)
                    {
                        if (t.Name == componentTypeName || t.FullName == componentTypeName)
                        { m_cached = c; m_cachedTypeName = componentTypeName; return c; }
                    }
                }
                return null;
            }

            /// <summary>Lists all readable public instance members of the bound component.</summary>
            public List<MemberEntry> GetAvailableMembers()
            {
                var result = new List<MemberEntry>();
                Component comp = GetComponent();
                if (!comp) return result;
                const BindingFlags f = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                Type type = comp.GetType();
                foreach (var p in type.GetProperties(f))
                    if (p.CanRead && p.GetIndexParameters().Length == 0)
                        result.Add(new MemberEntry(p.Name, p.PropertyType, "prop"));
                foreach (var fi in type.GetFields(f))
                    result.Add(new MemberEntry(fi.Name, fi.FieldType, "field"));
                result.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                return result;
            }

            public struct MemberEntry
            {
                public string name;
                public Type   type;
                public string kind; // "prop" / "field"
                public MemberEntry(string n, Type t, string k) { name = n; type = t; kind = k; }
            }
        }

        [Serializable]
        public class PlaceholderFormat
        {
            [Tooltip("Key without braces, e.g. 'Time.time'")]
            public string placeholder;
            [Tooltip("C# format string, e.g. 'F2', 'D4', 'N0', 'P1'")]
            public string format;
        }

        /// <summary>
        /// Binds to another TMP_DynamicText component.
        /// Use {alias} in the template to embed its fully resolved text.
        /// </summary>
        [Serializable]
        public class DynamicTextBinding
        {
            [Tooltip("Short name used in template: 'score' → {score}")]
            public string alias = string.Empty;

            [Tooltip("The TMP_DynamicText component whose resolved text will be embedded.")]
            public TMP_DynamicText source;

            public bool IsValid() => source != null && !string.IsNullOrEmpty(alias);

            /// <summary>Returns the current resolved text of the linked TMP_DynamicText.</summary>
            public string GetResolvedText()
            {
                if (!source) return string.Empty;
                // Trigger a fresh evaluation and return the rendered text.
                source.Refresh();
                var tmp = source.GetComponent<TextMeshProUGUI>();
                return tmp ? tmp.text : string.Empty;
            }
        }
    }
}