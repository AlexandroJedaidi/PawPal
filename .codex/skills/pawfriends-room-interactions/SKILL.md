---
name: pawfriends-room-interactions
description: Use when implementing PawFriends room interactions involving the current living-room assets: Ball_2, Bone_1, CatToy, ScratchingPostBig, Bed_1, bowl-like objects, petting, praise/scold, toys, feeding, drinking, player/camera attention, interest points, and interaction-driven dog reactions.
---

# PawFriends Room Interactions

This skill is for interaction systems in the PawFriends living-room pet simulation.

## Known Interaction Candidates

The current scene includes objects with names similar to:

```text
Bone_1
Ball_2
CatToy
ScratchingPostBig
Bed_1
BeadBowl / bowl-like object
BallHole_1
Camera
```

Inspect the scene/assets first. Do not assume exact names are final.

## Goal

Create modular interactions that let the dog react to existing room assets.

Examples:

- dog notices `Ball_2` and plays
- dog picks/looks/sniffs `Bone_1`
- dog goes to `Bed_1` when tired
- dog reacts to bowl/food when hungry
- dog looks toward player/camera when called
- dog reacts positively to praise/petting
- dog reacts negatively to scolding

## Preferred Components

```text
Assets/Scripts/Dog/Interaction/DogInteractionReceiver.cs
Assets/Scripts/Dog/Interaction/DogInteractionType.cs
Assets/Scripts/Dog/Interaction/DogInterestPoint.cs
Assets/Scripts/Dog/Interaction/DogToy.cs
Assets/Scripts/Dog/Interaction/DogBowl.cs
Assets/Scripts/Dog/Interaction/DogBed.cs
Assets/Scripts/Dog/Config/DogInteractionConfig.cs
```

## Interaction Flow

Use this flow:

```text
Player/Object sends interaction
DogInteractionReceiver receives it
DogNeeds/DogMood are modified
DogBrain receives a stimulus
DogNavigator may move to a target
DogAnimationController plays a reaction
```

Do not put all interaction logic inside the toy or bed object.

## Interaction Types

Start with:

```text
Pet
Brush
Feed
GiveWater
ShowToy
ThrowToy
CallName
Praise
Scold
InviteToBed
PointAtInterest
```

## DogInterestPoint

A room object can become an interest point.

Fields:

```text
InterestType
Priority
UsableRadius
LookAtTarget
CooldownSeconds
CanUseWhenHungry
CanUseWhenTired
CanUseWhenBored
AnimationHint
```

Types:

```text
Toy
Bed
FoodBowl
WaterBowl
Window
IdleSpot
Player
ScratchPost
```

Likely mappings:

```text
Ball_2 -> Toy
Bone_1 -> Toy
CatToy -> Toy
ScratchingPostBig -> ScratchPost or Toy
Bed_1 -> Bed
BeadBowl / bowl object -> FoodBowl or WaterBowl
Camera/player target -> Player
```

## Need and Mood Effects

Examples:

```text
Pet -> increases Affection, decreases stress
Praise -> increases Affection and Confidence
Scold -> decreases Affection/Confidence, interrupts current behaviour
Feed -> improves Hunger if near bowl/food
GiveWater -> improves Thirst if near bowl/water
ShowToy -> lowers Boredom and can trigger GoToToy
InviteToBed -> pushes GoToBed if Energy is low
```

Use ScriptableObject configs for numeric effects.

## Play Mode Testing

When implementing interactions, include test steps like:

```text
1. Add DogInteractionReceiver to the dog.
2. Add DogInterestPoint to Ball_2, Bone_1, Bed_1, and bowl object.
3. Assign type and usable radius.
4. In Play Mode, trigger interactions from context menu or debug component.
5. Verify dog state, need values, and animation request change.
```

## Do Not Do

- Do not rename the scene objects automatically.
- Do not require custom UI before interactions can be tested.
- Do not directly manipulate Animator from toy/bed/bowl scripts.
- Do not make hard dependencies on one specific prefab path unless requested.
