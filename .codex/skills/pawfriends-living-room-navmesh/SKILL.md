---
name: pawfriends-living-room-navmesh
description: Use when implementing PawFriends living-room movement: dog walkable/runnable areas, NavMeshAgent setup, random indoor wandering, playable area bounds, path validation, obstacle avoidance around furniture, and movement to toys, bed, bowl, or player/camera targets.
---

# PawFriends Living-Room NavMesh Movement

The PawFriends scene is a furnished indoor room with a dog, sofa, TV wall, bed, toys, bowl-like objects, cat tree/scratching post, window areas, and many apartment corner objects.

The project includes `com.unity.ai.navigation`, so use NavMesh-first movement.

## Goal

Make the dog move naturally inside the room without walking through furniture or leaving the playable area.

Movement should support:

- idle-to-wander transitions
- wandering to random valid points
- walking to `DogInterestPoint` objects
- going to toys such as `Ball_2`, `Bone_1`, `CatToy`
- going to `Bed_1`
- going to bowl-like objects such as `BeadBowl`
- approaching the player/camera area
- stopping and facing a target

## Preferred Components

```text
Assets/Scripts/Dog/Movement/DogNavigator.cs
Assets/Scripts/Dog/Movement/DogWanderController.cs
Assets/Scripts/Dog/Movement/DogPlayableArea.cs
Assets/Scripts/Dog/Interaction/DogInterestPoint.cs
Assets/Scripts/Dog/Config/DogMovementConfig.cs
```

## NavMeshAgent Rules

`DogNavigator` should wrap `NavMeshAgent`.

Expose methods/properties like:

```csharp
bool MoveTo(Vector3 target);
void Stop();
bool HasReachedDestination { get; }
bool IsMoving { get; }
bool TryGetRandomReachablePoint(out Vector3 point);
bool CanReach(Vector3 target);
float CurrentSpeed { get; }
```

Use:

- `NavMesh.SamplePosition`
- `NavMesh.CalculatePath` if path completeness matters
- `agent.remainingDistance`
- `agent.pathPending`
- `agent.velocity.magnitude`

## Random Wandering

When choosing a random wander point:

- choose around a configurable center or current position
- validate on NavMesh
- avoid points too close to the dog
- avoid points outside the living room
- attempt multiple samples before failing
- fail gracefully to Idle if no valid point is found

Inspector values:

```text
wanderRadius
minWanderDistance
sampleDistance
maxSampleAttempts
idlePauseAfterArrival
turnToTargetSpeed
```

## Playable Area

If the scene does not yet have a clean NavMesh setup, create a `DogPlayableArea` helper script that can define a box/radius area for random destination sampling.

Do not rely on `Terrain` alone as the playable area, because the room has furniture and apartment meshes.

## Interest Point Movement

Movement to room objects should use `DogInterestPoint` rather than hardcoded GameObject names.

Suggested mappings:

```text
Ball_2 -> Toy
Bone_1 -> Toy
CatToy -> Toy
Bed_1 -> Bed
BeadBowl / bowl object -> FoodBowl or WaterBowl
Window-side transforms -> LookAtInterestPoint / IdleSpot
```

The dog should stop at a configurable usable radius instead of trying to stand exactly at the object's pivot.

## Animator Integration

Movement scripts should not directly play idle/eat/sleep animations.

They may expose speed to `DogAnimationController` or `DogBrain`, but Animator parameter writes belong in `DogAnimationController`.

## Unity Editor Setup To Mention

When creating movement code, tell the user to verify:

```text
- floor/walkable surfaces are included in NavMesh baking
- furniture obstacles block or carve walkable areas where needed
- dog GameObject has NavMeshAgent
- NavMeshAgent radius/height fit the dog model
- stopping distance is not zero
- dog model root orientation matches movement direction
```

## Failure Handling

If NavMeshAgent is missing:

- log one clear warning
- disable movement behaviour or use a simple fallback only if explicitly supported

If no valid path is found:

- return false
- do not throw every frame
- let `DogBrain` choose another behaviour
