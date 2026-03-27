# CustomTMPro

**Powerful dynamic text system for TextMeshPro in Unity**

A clean and editor-friendly solution that lets you create rich, automatically updating UI texts using simple placeholder syntax like `{player.health}` or `{Application.version}`.

---

## Features

- **Simple template syntax** — Use `{variable}` or `{alias.member}` directly in the text
- **Component Bindings** — Bind any public field/property from any component on any GameObject
- **Dynamic Text Nesting** — Reference other `TMP_DynamicText` components (great for composed UI)
- **Static Unity properties** — Built-in support for `Application.version`, `Time.time`, `Screen.width`, `SystemInfo.deviceModel`, etc.
- **Custom format specifiers** — Support for C# formatting (`:F2`, `:D4`, `:P1`, etc.)
- **Live Preview** in the Inspector
- **Update Every Frame** option (perfect for timers, FPS counters, health bars, etc.)
- **Code-side variables** — Register custom values from scripts with `RegisterVariable()`
- **Beautiful custom Editor** with autocomplete, placeholder chips, member dropdowns, and snippet insertion
- **One-click creation** via GameObject menu
- Works in **Edit Mode** and **Play Mode**

---

## Installation

1. Copy the files into your Unity project

2. Make sure you have **TextMeshPro** package installed (via Package Manager).

3. The scripts will automatically appear under the correct namespaces.

---

## Quick Start

### 1. Create a Dynamic Text

Right-click in the Hierarchy → **GameObject > UI > Dynamic Text - TextMeshPro**

Or use the top menu: **GameObject > UI > Dynamic Text - TextMeshPro**

This will create a fully configured `TextMeshProUGUI` + `TMP_DynamicText` component (and a Canvas/EventSystem if needed).

### 2. Set up the Template

In the Inspector, write your template, for example:

```
Player: {player.name}
Level: {player.level}
Health: {player.health:F0} / {player.maxHealth}
Time: {Time.time:F1}s
Version: {Application.version}
```

### 3. Add Bindings

#### Component Bindings
- Click **＋ Add Binding**
- Set **Alias** → `player`
- Drag your player GameObject
- Select the component (e.g. `PlayerStats`)
- Choose the default member (optional)

#### Dynamic Text Bindings (optional)
- Reference another `TMP_DynamicText` to embed its resolved text

---

## Template Syntax

| Placeholder                        | Description                                      |
|------------------------------------|--------------------------------------------------|
| `{Application.version}`            | Static Unity property                            |
| `{Time.time:F2}`                   | With format specifier                            |
| `{player}`                         | Uses default `memberName` from binding           |
| `{player.health}`                  | Specific field/property                          |
| `{Health.current}`                 | Component on the same GameObject                 |
| `{score}`                          | Another `TMP_DynamicText` component              |
| `{MyCustomVar}`                    | Registered via `RegisterVariable()`              |

---

## Public API

```csharp
// Refresh text manually
dynamicText.Refresh();

// Register a custom variable available everywhere
TMP_DynamicText.RegisterVariable("gold", () => player.Gold.ToString());

// Namespace resolvers (advanced)
TMP_DynamicText.RegisterNamespaceResolver("MySystem", key => ResolveMyValue(key));
```

---

## How It Works (Resolution Order)

1. Code-registered variables (`RegisterVariable`)
2. Dynamic Text Bindings (other `TMP_DynamicText`)
3. Named Component Bindings (`{alias.member}`)
4. Components on the same GameObject (`{ComponentType.member}`)
5. Static Unity types (`Application`, `Time`, `Screen`, etc.)

---

## Requirements

- Unity 2020.3 or newer (recommended)
- TextMeshPro (official Unity package)
- Works with both UGUI and UI Toolkit? (UGUI only — this is for `TextMeshProUGUI`)

---

## Folder Structure (recommended)

```
Assets/
└── Plugins/
    └── CustomTMPro/
        ├── TMP_DynamicText.cs
        └── Editor/
            ├── TMP_DynamicTextEditor.cs
            └── TMP_DynamicTextCreator.cs
```

---

## License

This project is open-source. Feel free to use, modify, and distribute it in your commercial or non-commercial projects.

---

## Contributing

Pull requests and suggestions are welcome!

If you improve the editor, add new features (e.g. localization support, animation triggers, rich text integration), or fix bugs — feel free to contribute.

---

**Made with ❤️ for the Unity community**

Enjoy building cleaner and more maintainable UI!
This README is professional, clear, and user-friendly — perfect for a public GitHub repository named **CustomTMPro**. 

You can copy-paste it directly. Let me know if you want a shorter version, more technical details, or screenshots section added!
