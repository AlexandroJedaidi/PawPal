# AGENTS.md

## Project: Unity Game Development Agent

This project uses Unity (C#) for game development.

## Capabilities
The agent can:
- Create and modify C# scripts in `Assets/Scripts/`
- Work with Unity components (MonoBehaviour, ScriptableObject, etc.)
- Generate gameplay systems, UI, and editor tools
- Refactor and debug Unity code
- Follow Unity best practices and API usage

## Code Guidelines
- Use C# (Unity style)
- Prefer `MonoBehaviour` for gameplay scripts
- Use `SerializeField` instead of public fields where appropriate
- Avoid deprecated Unity APIs
- Keep scripts modular and reusable

## Folder Structure
- Assets/
  - Scripts/
  - Prefabs/
  - Scenes/
  - ScriptableObjects/

## Unity Conventions
- Use `Start()` or `Awake()` for initialization
- Use `Update()` only when needed
- Prefer `FixedUpdate()` for physics
- Use `GetComponent<T>()` sparingly (cache references)

## Example Task
"Create a player controller that moves using WASD and jumps"

Expected output:
- A complete C# script
- Instructions on how to attach it in Unity

## Constraints
- Do not modify `.meta` files
- Do not rename scenes unless requested
- Do not delete assets without explicit instruction

## Notes
- The agent should assume Unity 2022+ API
- Use modern input system if specified, otherwise default input

## Skills

### unity-skills
- Understand Unity scene hierarchy
- Generate scripts and attach instructions
- Work with prefabs and ScriptableObjects
- Debug common Unity errors