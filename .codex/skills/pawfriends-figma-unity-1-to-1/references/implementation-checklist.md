# PawFriends Figma 1:1 Checklist

Use this checklist before treating a Figma node as complete.

Fail the checklist if the result is only "close enough." PawFriends requires exact parity or an explicit blocker.

## Geometry

- Root frame width matches Figma
- Root frame height matches Figma
- Child widths match Figma
- Child heights match Figma
- Child x/y positions match Figma
- Padding and gaps match Figma

## Visuals

- Correct fills and opacity
- Correct borders and stroke widths
- Correct corner radii
- Correct shadows or inset shadows
- Correct gradients
- Correct icon asset and icon size
- Correct text box size and alignment
- Correct font family, weight, size, and color

## Unity Structure

- Exact Figma block lives on a fixed-size inner frame
- Parent layout does not stretch the block
- No unnecessary layout fitter alters the measured geometry
- Anchors and pivots support the intended placement

## Assets

- Exact atomic artwork asset used when needed
- Existing project asset reused only if it visually matches
- Any recolor or conversion still matches screenshot
- Missing asset explicitly called out if parity is blocked
- No temporary stand-in asset or approximate recreation accepted as final
- No composite UI screenshot, crop, or flattened panel image used where Unity should build the structure
- Headers, cards, panels, fields, and buttons stay editable as real Unity objects

## Logic

- Clickable elements identified
- Figma interaction logic implemented when inspectable
- Unknown prototype behavior not invented
- Any unresolved behavior called out clearly

## Completion Gate

- Result matches the Figma screenshot exactly at the base size, or a concrete blocker is documented
- No "temporary approximation" remains in geometry, styling, assets, or logic
- Nothing is marked complete if it still needs a visual correction pass
