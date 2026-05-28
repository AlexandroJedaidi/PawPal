---
name: pawfriends-figma-unity-1-to-1
description: Exact PawFriends Figma-to-Unity uGUI reconstruction workflow. Use when the user provides a PawFriends Figma link, node, section, widget, navbar, card, modal, or screen and wants a 1:1 implementation in the PawFriends Unity project. Use for strict visual parity at the Figma base size, exact frame dimensions, exact child positions, exact asset treatment, and interaction logic that matches the Figma design or prototype. Do not use for loose redesigns, approximations, temporary stand-ins, or responsive reinterpretations.
---

# PawFriends Figma Unity 1:1

Rebuild PawFriends Figma nodes as literal Unity uGUI implementations. Match the Figma node at the project base size first, then preserve that exact geometry in Unity instead of "cleaning it up" with generic layout systems.

Approximation is never acceptable in this workflow. Do not ship "close enough" visuals, placeholder styling, or temporary component reconstructions when the Figma source provides exact geometry, assets, states, or screenshots.

Use this skill together with:
- `pawfriends-project-context`
- `pawfriends-safe-unity-workflow`

## Workflow

1. Read the Figma node with MCP.
2. Pull both structure and image context before implementing:
   - `get_design_context`
   - `get_metadata`
   - `get_screenshot`
3. Extract the exact values that matter:
   - root frame width and height
   - every direct child width, height, x, y
   - text box sizes and positions
   - icon sizes and positions
   - border widths, radii, shadow direction, fills, gradients
   - spacing between items
   - whether the node behaves like a fixed card, full-width section, overlay, sheet, or screen
4. Implement the node in Unity from those measurements, not from memory and not from a "similar" pattern already in the repo.
5. Validate against the Figma screenshot after implementation and keep correcting until it matches exactly or until a concrete blocker is identified.
6. If exact parity is blocked by a missing asset, missing prototype data, or export limitation, stop and call out the blocker explicitly instead of approximating.

## Non-Negotiables

- Treat Figma as the source of truth.
- Build the root node at the exact Figma size first.
- Preserve the exact child layout inside that root.
- Never do approximation passes, even temporary ones.
- Never flatten editable UI into screenshots, cropped frame images, or composite exported panel images.
- Never present a reconstruction as complete if it is only visually similar.
- Do not stretch Figma widgets because a parent layout group wants to expand them.
- Do not substitute approximate geometry when exact geometry is available.
- Do not replace exact asset treatments with procedural shapes unless the exact visual cannot be reproduced any other way, and if that fallback still does not match the screenshot exactly, treat it as blocked rather than acceptable.
- Do not import composite UI surfaces such as whole cards, headers, panels, fields, buttons, list rows, or state snapshots just because they are easy to export from Figma.
- Build reusable UI structure from Figma measurements so the result remains editable in Unity later.
- Do not invent responsive behavior unless the user asks for it.
- Do not invent interaction logic. Implement prototype behavior only when it is actually observable from the Figma data or prototype. If the logic is not inspectable, say that clearly and ask for the prototype flow or behavior.

## Unity Layout Rules

- PawFriends targets the same mobile base layout as the Figma files, so default to literal measurement transfer.
- Default target is the project's Figma-sized mobile base layout.
- Match the node at base size before doing anything adaptive.
- For fixed widgets inside a flowing screen, use a fixed-size inner frame and let the outer wrapper participate in parent layout.
- Prefer manual `RectTransform` placement for 1:1 blocks.
- Use `HorizontalLayoutGroup` or `VerticalLayoutGroup` only when the Figma node itself is clearly an evenly spaced auto-layout row/column and the layout group will not alter the measured geometry.
- Never let `childForceExpandWidth` or `childForceExpandHeight` distort a measured Figma block.
- Do not use `ContentSizeFitter` on the exact Figma block itself if it changes the measured size.
- Use wrapper objects:
  - outer wrapper for screen flow
  - inner frame for exact Figma size and child placement

## Asset Rules

- Use Figma exports only for atomic artwork that is meant to remain artwork:
  - icons
  - product illustrations
  - mascot art
  - decorative vector badges
- Do not use Figma exports for composite UI construction:
  - panels
  - headers
  - fields
  - buttons
  - tabs
  - cards
  - list rows
  - full widget states
  - full screen regions
- Rebuild composite UI from exact Figma geometry, colors, radii, borders, shadows, and spacing in Unity.
- Reuse existing project assets only when they visually match the Figma node.
- If reusing an existing asset requires recoloring or alpha-mask conversion, ensure the result still matches the Figma screenshot.
- If the needed asset is missing or the MCP export is unavailable, state that explicitly rather than silently approximating.
- Do not replace a missing exact asset with a homemade approximation just to keep moving.

## Interaction Rules

- Inspect whether the Figma node includes clickable controls, carousels, navigation arrows, toggles, or other interactive states.
- Implement the interaction logic if the behavior is visible in the Figma structure, variants, or prototype context.
- If only the visuals are available and the behavior is not inspectable, do not fabricate the logic. Mark the behavior as unresolved and ask for the prototype flow or user intent.
- When behavior is implemented, keep the Unity logic aligned with the visual structure from the same node.

## Validation Pass

Before finishing:

1. Compare the Unity block against the Figma screenshot.
2. Check:
   - root size matches
   - each child size matches
   - each child position matches
   - text alignment and box size match
   - corner treatment matches
   - icon scale and placement match
   - the block is not being stretched by parent layout
3. If the Unity result is visibly different, keep correcting it instead of calling it done.
4. If the result is still "close but not exact," it is not done.

Use [references/implementation-checklist.md](references/implementation-checklist.md) as the final audit list when a node is complex.

## Response Behavior

When using this skill:

- say when an implementation is exact versus inferred
- call out missing prototype logic instead of guessing
- mention any asset limitation explicitly
- keep iterating on the same node until the Figma parity problem is actually solved
- never describe an approximation as acceptable for PawFriends
- when blocked from exact parity, say what is missing and stop short of claiming completion
