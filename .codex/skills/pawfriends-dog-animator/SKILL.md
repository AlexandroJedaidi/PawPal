---
name: pawfriends-dog-animator
description: Use when connecting PawFriends dog behaviour to the existing dog Animator Controller and animations: idle, sit, lie, walk/run, bark, sniff, scratch, sleep/rest, toy reactions, bed/bowl interactions, Animator parameters, triggers, blend values, and safe animation wrappers.
---

# PawFriends Dog Animator Integration

The PawFriends project already has dog animations/Animator assets visible in Unity. The Animator view in the current project shows dog state-machine style animation states for common dog actions such as idle, sitting/lying/resting, movement, and reactions.

Do not assume exact parameter or state names. Inspect actual Animator Controllers and animation assets before writing code.

## Goal

Create a safe animation wrapper so dog AI code can request animation intents without hardcoding many Animator details throughout the project.

Preferred wrapper:

```text
Assets/Scripts/Dog/Animation/DogAnimationController.cs
Assets/Scripts/Dog/Animation/DogIdleController.cs
Assets/Scripts/Dog/Config/DogIdleProfile.cs
```

## Required Pattern

Only `DogAnimationController` should write to `Animator` parameters.

Other systems should call high-level methods like:

```csharp
SetMovementSpeed(float speed);
PlayIdle(DogIdleAction action);
SetResting(bool value);
SetSleeping(bool value);
TriggerBark();
TriggerHappyReaction();
TriggerPetReaction();
TriggerToyReaction();
TriggerEat();
TriggerDrink();
```

## First Step: Inspect Existing Animator

Before creating or changing scripts, inspect:

```text
Assets/**/Dog*.controller
Assets/**/*.controller
Assets/**/*.anim
Assets/**/*.fbx
Assets/**/Animations/**
```

Find actual:

- Animator Controller path
- parameter names
- state names
- clips for idle/sit/lie/sleep/walk/run/bark/reactions
- any existing dog animation scripts

## Parameter Safety

Use `Animator.StringToHash`.

Validate parameters once at startup.

If a parameter is missing:

- warn once
- do not spam every frame
- do not crash gameplay

Do not rename Animator states/clips automatically.

## Expected Animation Intents

Support these intents if matching animations exist:

```text
StandIdle
SitIdle
LieIdle
Sleep
Walk
Run
Sniff
Scratch
Yawn
LookAround
LookAtPlayer
TailWag
Bark
HappyReact
SadReact
PetReact
ToyReact
Eat
Drink
```

If an animation does not exist yet, keep the intent but map it to a safe fallback such as `StandIdle`.

## Suggested Animator Parameters

Use existing parameters if present. Only propose these if missing:

```text
Speed float
IdleVariant int
Mood float
Energy float
IsResting bool
IsSleeping bool
IsEating bool
IsPlaying bool
Bark trigger
Sniff trigger
Scratch trigger
Yawn trigger
TailWag trigger
HappyReact trigger
PetReact trigger
ToyReact trigger
```

Do not create code that requires all of these to exist unless also adding clear setup instructions.

## Idle Selection

`DogIdleController` chooses idle intent; `DogAnimationController` translates intent to Animator calls.

Idle selection should use:

- weights
- cooldowns
- last-action suppression
- energy/mood/boredom modifiers
- interrupt support

## Movement Animation

For walking/running:

- read speed from `NavMeshAgent.velocity.magnitude` through `DogNavigator`
- smooth speed before writing to Animator
- prefer `Speed` float/blend tree over walk/run triggers

## Animation Events

Use animation events only for timing-specific moments:

```text
bark sound timing
food bite finished
toy bite/catch moment
trick completed
sleep loop entered
```

Do not depend on animation events for core state selection.

## Output Requirements

After changes, explain:

```text
- Which Animator parameters are required
- Which are optional
- Which existing clips/states were detected
- Which scripts were added
- What to assign on the dog GameObject
- How to test in Play Mode
```
