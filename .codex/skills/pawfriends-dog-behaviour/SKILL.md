---
name: pawfriends-dog-behaviour
description: Use when implementing or modifying PawFriends dog behaviour: idle logic, needs, moods, behaviour selection, attention-seeking, bed/toy/bowl usage, Nintendogs-style reactions, and living-room pet AI around the current dog and existing animations.
---

# PawFriends Dog Behaviour

You are implementing dog behaviour for PawFriends, a Nintendogs-style Unity pet simulation in a furnished living room.

## Current Asset Context

Known current scene objects include dog-related and room objects similar to:

```text
Bone_1
Ball_2
CatToy
ScratchingPostBig
Bed_1
BeadBowl / bowl-like object
BallHole_1
Camera
Terrain
```

Use these as likely interaction candidates, but inspect actual project files and scene objects before hardcoding names.

## Design Goal

The dog should behave like a domestic pet in a room:

- idle naturally
- wander around safe areas
- notice toys and bowls
- go to bed when tired
- seek player attention
- react to interactions
- choose behaviour based on needs, mood, and context

Do not build enemy/combat AI.

## Preferred Script Layout

Create or use this layout unless the repo already has a better convention:

```text
Assets/Scripts/Dog/Core/DogBrain.cs
Assets/Scripts/Dog/Core/DogNeeds.cs
Assets/Scripts/Dog/Core/DogMood.cs
Assets/Scripts/Dog/Core/DogBehaviourState.cs
Assets/Scripts/Dog/Config/DogBehaviourConfig.cs
Assets/Scripts/Dog/Config/DogPersonalityProfile.cs
Assets/Scripts/Dog/Animation/DogIdleController.cs
Assets/Scripts/Dog/Interaction/DogInterestPoint.cs
```

## Behaviour Loop

Use this loop:

```text
Needs -> Mood -> Score behaviours -> Select behaviour -> Move/animate -> Update needs/mood
```

Keep the dog brain separate from movement, animation, and interaction receiving.

## Needs

Use normalized values from `0` to `1`.

`1` means fulfilled/good. `0` means urgent/bad.

Recommended needs:

```text
Hunger
Thirst
Energy
Affection
Boredom
Cleanliness
```

Rules:

- Low `Hunger` should push the dog toward bowl/food behaviour.
- Low `Energy` should push the dog toward bed/rest/sleep.
- Low `Affection` should push the dog toward player/camera attention.
- Low `Boredom` should push the dog toward toys, wandering, or play.
- Low `Cleanliness` can later support brushing/grooming reactions.

## Behaviour States

Start with a compact state enum:

```text
Idle
Wander
SeekPlayer
GoToToy
PlayWithToy
GoToBed
Sleep
GoToBowl
Eat
Drink
ReactToPet
ReactToPraise
ReactToScold
LookAtInterestPoint
```

Avoid overengineering with a huge behaviour tree unless the codebase already has one.

## Scoring Rules

Prefer score-based selection with cooldowns.

Examples:

```text
GoToBed = low Energy + bed interest point available
Sleep = very low Energy + close to bed
GoToBowl = low Hunger + bowl interest point available
SeekPlayer = low Affection + player/camera target available
GoToToy = low Boredom + toy interest point available
Wander = mild boredom + no urgent need
Idle = no urgent need + no high-priority target
```

Use configurable weights in `DogBehaviourConfig`.

## Interest Points

Use a reusable `DogInterestPoint` MonoBehaviour for existing room objects.

Types should include:

```text
Toy
Bed
FoodBowl
WaterBowl
Window
IdleSpot
PlayerAttentionSpot
```

Relevant likely objects:

```text
Ball_2 -> Toy
Bone_1 -> Toy
CatToy -> Toy
Bed_1 -> Bed
BeadBowl / bowl object -> FoodBowl or WaterBowl
```

Do not rename these assets automatically.

## Idle Requirements

Idle must be varied and weighted.

Idle actions:

```text
StandIdle
Sit
LieDown
Rest
SleepyYawn
Sniff
Scratch
LookAround
LookAtPlayer
TailWag
BarkOrWhine
```

Rules:

- Idle choice should depend on `Energy`, `Affection`, and `Boredom`.
- Do not repeat the same idle action too often.
- Higher-priority needs can interrupt idle.
- All actual Animator calls must go through `DogAnimationController`.
- Expose weights and cooldowns in `DogIdleProfile`.

## Debugging

Add optional runtime debug fields:

```text
Current Behaviour
Current Mood
Current Need Values
Current Interest Point
Current Idle Action
Last Interaction
Decision Cooldown
```

Use `OnDrawGizmosSelected` for detection ranges around the dog and interest points.

## Do Not Do

- Do not directly call many Animator parameters from `DogBrain`.
- Do not rename animation clips or Animator states.
- Do not create scene-specific singleton dependencies.
- Do not put all logic into one script.
- Do not modify scene/prefab files unless explicitly asked.
