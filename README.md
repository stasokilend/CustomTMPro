# TMP Dynamic Text

**Powerful dynamic text system for TextMeshPro in Unity**

Easily create rich, data-driven UI texts using simple placeholders like `{player.health}`, `{Application.version}`, or `{Time.time:F2}` — no more manual string formatting in scripts!

---

## ✨ Features

- **Simple placeholder syntax**: `{alias.member}` or `{Application.version}`
- **Live preview** directly in the Inspector
- **Component bindings** — bind any public field/property from any GameObject
- **Dynamic Text nesting** — embed one `TMP_DynamicText` inside another
- **Format support** — `:F2`, `:D4`, `:P1`, etc.
- **Custom variables** via code (`RegisterVariable`)
- **Update every frame** option (perfect for timers and counters)
- **One-click creation** via GameObject menu
- **Beautiful custom Editor** with autocomplete, chips, and snippets
- Works in **Editor** and **Build**
- Fully compatible with **TextMeshPro**

---

## Installation

1. Copy the three scripts into your Unity project:
   - `TMP_DynamicText.cs`
   - `TMP_DynamicTextEditor.cs`
   - `TMP_DynamicTextCreator.cs`

2. Make sure **TextMeshPro** is installed (Window → TextMeshPro → Import TMP Essential Resources).

3. The scripts will automatically appear under:
   **GameObject → UI → Dynamic Text - TextMeshPro**

---

## Quick Start

1. Right-click in Hierarchy → **UI → Dynamic Text - TextMeshPro**
2. A new TextMeshPro object with `TMP_DynamicText` component will be created
3. In the **Template** field, write your text with placeholders:

   ```text
   Player: {player.name}
   Health: {player.health} / {player.maxHealth}
   Score: {score.value:F0}
   Version: {Application.version}
   Time: {Time.time:F1}s
   ```

4. Add **Component Bindings**:
   - Set Alias = `player`
   - Drag your Player GameObject
   - Select the component and member (e.g. `Health` script → `currentHealth`)

5. Click **Refresh Now** or enable **Update Every Frame**

Done! Your text will update automatically.

---

## Usage Examples

### Basic
```
Score: {score}
```

### With formatting
```
Time: {Time.time:F2}s
Health: {player.health:F0}%
```

### Component on same GameObject
If you have a `PlayerStats` component on the same object:
```
Level: {PlayerStats.level}
```

### Nested Dynamic Text
You can reference another `TMP_DynamicText` component:
- Add a **Dynamic Text Binding** with alias `header`
- Use in template: `Status: {header}`

---

## Public API

```csharp
// Register custom variable accessible from any TMP_DynamicText
TMP_DynamicText.RegisterVariable("gold", () => player.Gold.ToString());

// Force refresh
dynamicText.Refresh();

// Change template at runtime
dynamicText.templateText = "New text with {player.health}";
```

---

## How It Works (Resolution Priority)

1. **Custom registered variables** (`RegisterVariable`)
2. **Dynamic Text Bindings** (other `TMP_DynamicText`)
3. **Named Component Bindings** (`{alias.member}`)
4. **Components on the same GameObject** (`{ComponentType.member}`)
5. **Static Unity types** (`{Application.version}`, `{Time.deltaTime}`, etc.)

---

## File Structure

```
Assets/
├── TMP_DynamicText.cs              ← Core runtime component
├── TMP_DynamicTextEditor.cs        ← Custom Inspector + Preview
└── TMP_DynamicTextCreator.cs       ← Menu item (GameObject → UI)
```

---

## Requirements

- Unity 2019.4 or newer
- TextMeshPro (Unity Package Manager)

---

## License

This project is open-source and free to use in any commercial or non-commercial project.

Feel free to modify and extend it.

---

## Created with ❤️ for Unity developers

Made to save you time writing boilerplate UI update code.

---

**Enjoy dynamic texts without the hassle!**

Any suggestions or improvements? Feel free to open an issue or PR.
