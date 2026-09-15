---
name: ui
description: Unity UI expert for menus, HUDs, screens, panels, buttons, labels, and all visual interface elements. Handles questions about UI in scenes or prefabs (how many elements, what exists, structure analysis), styling changes (colors, borders, backgrounds, fonts, spacing, rounded corners), layout adjustments, and UI generation. Routes to uGUI or IMGUI based on project context. Use for ANY request to build, edit, or understand game UI (menus, HUDs, settings or pause screens) when no framework is named: consult this skill to detect which UI system the project uses before writing any UI code, even for a request that looks simple enough to build directly.
---

Determine the appropriate UI system for the project and route to the correct specialized skill.

## When to Route vs Answer Directly

**Route to a specialized skill when:**
- User wants to understand, edit, or generate specific UI elements
- User references specific files or UI objects
- User asks for UI changes or creation

**Answer directly (without routing) when:**
- User asks comparative/educational questions ("What's the difference between UI Toolkit and uGUI?")
- User asks about UI system capabilities or recommendations ("Should I use UITK or uGUI for mobile?")
- User needs conceptual explanation of Unity UI architecture

## Routing Logic

**Step 1: Check for explicit file references or keywords:**

| User mentions | Route to |
|---------------|----------|
| `.uxml` or `.uss` files (including in `/Editor/`) | Inspect directly; no dedicated project skill |
| "UI Toolkit", "UITK", "UIElements", "CreateGUI" | Inspect directly; no dedicated project skill |
| Canvas prefabs/objects, `.prefab` with UI | `ui-ugui` |
| "uGUI", "Canvas", "RectTransform", "legacy UI" | `ui-ugui` |
| "IMGUI", "OnGUI", "OnInspectorGUI", "immediate mode" | `ui-imgui` |
| Figma URL (`figma.com/design/...`), "Figma", "import from Figma" | Not available — see below |

**For editor-related requests (EditorWindow, custom inspector, PropertyDrawer):**
- If no explicit UI system mentioned → **Go to Step 2** to detect project's editor UI system
- If no existing pattern is detected, default to `ui-imgui` for new editor UI in this project
- Use `ui-imgui` for existing or new project editor tools unless another framework is explicitly required

If explicit file or keywords found, activate the corresponding skill immediately.

**Step 2: If ambiguous, detect from project:**

Search the project to determine which UI system is in use:

| Look for | Indicates |
|----------|-----------|
| `.uxml` or `.uss` files (including in `/Editor/`) | Existing UI Toolkit code; inspect directly |
| `UIDocument` components in scenes | UI Toolkit runtime; inspect directly |
| Editor scripts with `CreateGUI()` method | UI Toolkit editor code; inspect directly |
| `Canvas` in scenes/prefabs | uGUI |
| `RectTransform` heavy usage | uGUI |
| Editor scripts with `OnGUI()` or `OnInspectorGUI()` | IMGUI (legacy editor) |

**Step 3: If still unclear, ask or default:**

- For existing projects: detect and follow whichever framework is already in use (Step 2)
- For new projects with no UI yet: ask the user whether they prefer uGUI or another explicitly requested framework
- For new runtime/game UI where the user has no preference: default to uGUI (`ui-ugui`)
- When the user mentions mobile/performance constraints or older Unity versions (pre-6.0): bias toward uGUI (`ui-ugui`)

## Request Types

Specialized skills handle three types of requests:

| Type | Examples |
|------|----------|
| **Understanding** | "What does this button do?", "How is this laid out?", "Explain this UI" |
| **Editing** | "Change this color", "Add a label here", "Fix this layout" |
| **Generation** | "Create a menu", "Make an inventory screen", "Build a settings panel" |

Route all types to the appropriate specialized skill based on the UI system.

## Available Sub-Skills

### Existing UI Toolkit code
- Third-party libraries may contain UI Toolkit runtime or editor code
- Inspect existing `.uxml`, `.uss`, `UIDocument`, or `CreateGUI()` implementations directly
- Do not introduce UI Toolkit into project-authored UI unless the user explicitly requests it

### uGUI — `ui-ugui`
- For projects using Unity's Canvas-based UI system
- **Understands**, **edits**, and **generates** Canvas hierarchies
- Uses Layout Groups for responsive design
- Default for new runtime/game UI when the user has no framework preference

### IMGUI — `ui-imgui`
- For editor tools using OnGUI/immediate mode
- Use for existing or new editor tools in this project
- **Understands**, **edits**, and **generates** EditorWindow, inspectors, PropertyDrawers built with OnGUI
- Not for runtime game UI — use uGUI

### Figma design import — not available here

Importing a Figma design requires Unity's Figma integration service, which only exists
inside Unity AI Assistant. There is no client-side equivalent, so do not promise it.

If the user brings a Figma URL, say the automated import is not available here and offer
the alternative: ask them to describe or screenshot the screen, then build it with the
appropriate framework skill above.

## Common Guidelines (All UI Systems)

### Scope Discipline

**Do only what is requested:**
- Question → answer without making changes
- Targeted edit → modify only what's specified
- Generation → create only requested files
- Don't proactively add scripts unless explicitly asked

**These do NOT imply scripts:**
- "proper buttons" → well-styled buttons
- "working UI" → valid UI that renders
- "menu screen" → visual layout only

### Conventions

**Follow project patterns first.** Search existing files before applying defaults.

| Type | Convention |
|------|------------|
| Element names | Follow project patterns, or camelCase |
| File organization | Match existing project structure |

### Workflow

1. **Determine UI system** — Use routing logic above
   - For Figma requests, tell the user the automated import is not available here, then
     work from their description or screenshot and continue with framework detection
2. **Activate specialized skill** — Route to `ui-ugui` or `ui-imgui`
3. **Skill handles request** — Understanding, editing, or generation as appropriate

## Handling Mixed Projects

Third-party Unity libraries may use UI Toolkit even when the project itself uses uGUI and IMGUI. When you detect this:

- **For runtime UI requests** (menus, HUDs, game screens) → Route to `ui-ugui`
- **For editor tool requests** (custom inspectors, editor windows) → Route to `ui-imgui`
- **For third-party UI Toolkit code** → Inspect the existing implementation directly; do not introduce UI Toolkit into project-authored UI without the user explicitly requesting it
