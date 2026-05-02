---
name: pawfriends-safe-unity-workflow
description: Use whenever Codex edits the PawFriends Unity project. Protects Unity scenes, prefabs, Animator Controllers, animation clips, materials, textures, .meta files, Git LFS assets, and prevents accidental changes to generated Unity folders or broken serialized references.
---

# PawFriends Safe Unity Workflow

PawFriends is a Unity project with many imported assets, animations, prefabs, materials, and scene references. Unity references depend heavily on `.meta` GUIDs and serialized fields.

## Do Not Touch Without Explicit Instruction

Avoid editing, moving, deleting, or regenerating these unless the user explicitly asks:

```text
*.unity
*.prefab
*.controller
*.anim
*.fbx
*.mat
*.asset
*.meta
ProjectSettings/*.asset
```

It is okay to inspect them. Be cautious about modifying them.

## Never Add These To Git

Do not create, edit for commit, or add:

```text
Library/
Logs/
UserSettings/
.vscode/
.plastic/
*.csproj
*.sln
```

## Safe Script Work

When adding or changing scripts:

- keep MonoBehaviour class names matching filenames
- preserve namespaces if the project uses them
- use `[SerializeField] private` for Inspector references
- do not rename serialized fields casually
- if a serialized field must be renamed, use `[FormerlySerializedAs]`
- keep Unity API calls on main thread
- avoid editor-only APIs in runtime scripts

Example:

```csharp
using UnityEngine.Serialization;

[FormerlySerializedAs("oldName")]
[SerializeField] private Transform newName;
```

## Scene/Prefab Safety

If a change requires scene setup, prefer explaining the setup rather than modifying scene/prefab files automatically.

For example, say:

```text
Add DogBrain, DogNavigator, DogAnimationController, and NavMeshAgent to the dog GameObject.
Assign the Animator reference from the dog model child.
Assign Bed_1, Ball_2, Bone_1, and bowl object as DogInterestPoint components.
```

Do not silently rewrite the scene.

## Git/LFS Safety

Large Unity assets are tracked by Git LFS. Do not remove or overwrite `.gitattributes` rules.

If editing text scripts only, do not include binary Unity assets in the same commit unless necessary.

## Compile Safety

Generated code should be compatible with Unity C#.

Avoid requiring newer C# features that may not be supported by Unity's compiler settings.

Prefer simple, readable code over clever abstractions.

## Before Finishing A Task

Run or recommend checks:

```text
- Unity Console has no compile errors
- dog GameObject has required components assigned
- Animator parameters exist or warnings are expected
- NavMeshAgent is present if movement is enabled
- no ignored folders appear in git status
```

## Required Summary After Edits

Always summarize:

```text
Files changed
Why each file changed
Unity setup needed
How to test
Potential risks
```
