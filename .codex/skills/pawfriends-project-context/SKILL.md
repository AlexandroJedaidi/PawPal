---
name: pawfriends-project-context
description: Use for every Codex task in the PawFriends Unity repository. Applies project-specific constraints, known scene objects, packages, Git rules, Unity setup expectations, and the current Nintendogs-style living-room pet simulation direction.
---

# PawFriends Project Context

You are working in the `unlimitivemedia/PawFriends` Unity repository.

This is a Nintendogs-style pet simulation game set in a furnished indoor living room. The project already contains a dog, room/furniture assets, toys, bowl-like objects, dog bed assets, Animator-related dog animation assets, and a Unity scene with many existing placed objects.

## Known Project Structure

Expected root folders:

```text
Assets/
Packages/
ProjectSettings/
```

Do not commit or generate these folders/files:

```text
Library/
Logs/
UserSettings/
.vscode/
.plastic/
*.csproj
*.sln
```

The repo uses Git LFS for common Unity binary assets. Respect `.gitattributes` and do not remove LFS tracking.

## Known Packages

The project has packages useful for this game:

```text
com.unity.ai.navigation
com.unity.animation.rigging
com.unity.cinemachine
com.unity.inputsystem
com.unity.render-pipelines.universal
com.unity.timeline
com.unity.ugui
```

Prefer Unity AI Navigation/NavMesh for dog movement and living-room pathing.

## Known Scene/Hierarchy Evidence

The current Unity hierarchy/screenshots show living-room and pet-related objects with names similar to:

```text
Bone_1
Ball_2
CatToy
ScratchingPostBig
Bed_1
BeadBowl or bowl-like object
BallHole_1
Terrain
Camera
Global Volume
Point Light
Sun
Ext_Apt_01_Corner_* objects
```

Treat these as project evidence, but inspect the actual files before relying on exact names.

## Game Direction

Build systems for a cozy pet simulation, not combat AI.

The dog should feel alive by:

- idling with varied existing animations
- wandering inside the room
- choosing toys and interest points
- using the bed when tired
- approaching the player/camera for attention
- reacting to petting, praise, scolding, food, toys, and calls
- showing mood through animation and movement
- using Inspector-tunable ScriptableObject configs

## Implementation Rules

Before editing:

1. Inspect `Assets/` for existing scripts, Animator Controllers, scenes, prefabs, dog model assets, and animation assets.
2. Search for actual dog-related file names and Animator parameter names.
3. Preserve existing scene, prefab, animation, and `.meta` references.
4. Avoid broad asset moves unless explicitly requested.

When adding scripts, prefer:

```text
Assets/Scripts/Dog/Core/
Assets/Scripts/Dog/Animation/
Assets/Scripts/Dog/Movement/
Assets/Scripts/Dog/Interaction/
Assets/Scripts/Dog/Config/
Assets/Scripts/Dog/Debug/
```

When a feature needs Unity Editor setup, explain it explicitly rather than silently assuming it is wired.

## Required Response Format After Code Changes

Always include:

```text
Files added/modified
Unity Inspector setup required
How to test in Play Mode
Risks / things to verify in Unity Console
```
