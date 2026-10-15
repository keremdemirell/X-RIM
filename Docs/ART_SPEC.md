# X-RIM art spec

*Generated from the code's starting values on 2026-10-11. To regenerate it from your tuning, run the Unity menu **XRim/Reports/Write Art Spec**; do not edit it by hand.*

Everything the game draws for the dummies, their weapons, the mechanical debris and the electric wall: sizes, pivots, joints, draw order, and how to name and deliver the files. The art replaces the placeholder shapes in Session 17.

## Ground rules

- **Units.** Sizes are in *arena units*, the game's own measure (the dummy's torso is 60 × 120). In Unity, 1 arena unit is 0.01 world units.
- **Pixels.** Draw at **4 px per arena unit**. The duel camera shows about 700 units of height, so a 1080-pixel-tall phone shows 1.5 px per unit, and 2.4 when the instant replay closes in; 4 leaves room for sharper screens. The *Pixels* columns are sizes at that density. Import the sprites with **Pixels Per Unit = 400**.
- **Facing.** Draw everything facing **right** (+X), standing upright. The right-hand dummy is the same art mirrored.
- **Pivots.** Every piece turns about a joint; put its pivot exactly there (given below as a point on the sprite).
- **Draw order.** Far-side limbs (the dummy's left arm and leg) behind the torso, the head over the torso, near-side limbs (its right arm and leg) in front, the held weapon over everything. Higher numbers draw on top.
- **No blood, ever** (GDD §2). Damage is bolts, nuts, springs, gears, sparks and hydraulic fluid.
- **Calibration markers.** Paint the yellow-and-black quartered disc of a crash-test dummy into the middle of each body piece, 80% of the piece's narrower side across (the *Marker* column): the markers show the hit zones, so pieces of one zone share a marker size. The placeholder draws them in the same spots.

## The dummy: 10 bodies

The current setting builds the dummy from 10 bodies (D2): each arm and leg in two pieces. Points are measured from the **pelvis** at (0, 0), standing, facing right, y up.

| Piece | Hit zone | Size (units) | Pixels | Pivot on the sprite | Pivot on the dummy | Draw order | Marker Ø (units) |
|---|---|---|---|---|---|---|---|
| Torso | Torso | 60 × 120 | 240 × 480 | bottom middle | (0, 0) | 0 | 48 |
| TorsoBottom | Torso (after a KO) | 60 × 60 | 240 × 240 | bottom middle | (0, 0) | 0 | 48 |
| TorsoTop | Torso (after a KO) | 60 × 60 | 240 × 240 | bottom middle | (0, 60) | 0 | 48 |
| Head | Head | Ø 50 | 200 × 200 | bottom middle (the neck) | (0, 120) | 10 | 40 |
| LeftArmUpper | LeftArm | 24 × 120 | 96 × 480 | top middle | (0, 100) | -20 | 19.2 |
| LeftArmLower | LeftArm | 24 × 120 | 96 × 480 | top middle | (0, -20) | -20 | 19.2 |
| RightArmUpper | RightArm | 24 × 120 | 96 × 480 | top middle | (0, 100) | 20 | 19.2 |
| RightArmLower | RightArm | 24 × 120 | 96 × 480 | top middle | (0, -20) | 20 | 19.2 |
| LeftLegUpper | LeftLeg | 28 × 90 | 112 × 360 | top middle | (0, 0) | -20 | 22.4 |
| LeftLegLower | LeftLeg | 28 × 90 | 112 × 360 | top middle | (0, -90) | -20 | 22.4 |
| RightLegUpper | RightLeg | 28 × 90 | 112 × 360 | top middle | (0, 0) | 20 | 22.4 |
| RightLegLower | RightLeg | 28 × 90 | 112 × 360 | top middle | (0, -90) | 20 | 22.4 |

### Joints

Each joint is the child piece's pivot. Angles are measured from the standing pose; a positive angle turns the child counter-clockwise (a hanging arm or leg swings forward).

| Joint | Child piece | Parent piece | Point on the dummy | Turns (degrees) |
|---|---|---|---|---|
| Neck | Head | Torso | (0, 120) | -30 to 30 |
| LeftShoulder | LeftArmUpper | Torso | (0, 100) | -120 to 220 |
| LeftElbow | LeftArmLower | LeftArmUpper | (0, -20) | 0 to 140 |
| RightShoulder | RightArmUpper | Torso | (0, 100) | -120 to 220 |
| RightElbow | RightArmLower | RightArmUpper | (0, -20) | 0 to 140 |
| LeftHip | LeftLegUpper | Torso | (0, 0) | -30 to 90 |
| LeftKnee | LeftLegLower | LeftLegUpper | (0, -90) | -130 to 0 |
| RightHip | RightLegUpper | Torso | (0, 0) | -30 to 90 |
| RightKnee | RightLegLower | RightLegUpper | (0, -90) | -130 to 0 |

### When a dummy is knocked out

At a KO the torso splits across its middle (GDD §11), 50% of the way up, at y = 60: **TorsoBottom** and **TorsoTop** replace the torso. Draw both halves with torn edges that meet at that line.

### Loose limbs

A severed arm or leg (GDD §12) keeps its pieces: it lies on the floor drawn with the same art, pivoting at the same end. Its torn end (the shoulder or the hip) then shows, so draw that end as a socket or torn metal. A severed limb picked up and swung is drawn as the held club (below).

### If the dummy is built from 6 bodies instead (D2)

Each arm and leg is one piece: LeftArm 24 × 240 (pivot top middle, at (0, 100)); RightArm 24 × 240 (pivot top middle, at (0, 100)); LeftLeg 28 × 180 (pivot top middle, at (0, 0)); RightLeg 28 × 180 (pivot top middle, at (0, 0)). Drawing each limb as one picture split at the elbow or knee serves both.

## Weapons and held items

A held item is drawn in its own frame: the **grip** at the origin, pointing right (+X). At rest the grip sits at the end of the hanging right arm, (0, -140) from the pelvis. Held items draw over everything (order 30). Keep each drawing inside its box: the box is exactly what can hit (GDD §6: the weapon's length and hit width).

| Item | Id | Size along × across (units) | Pixels | Grip (pivot on the sprite) | Notes |
|---|---|---|---|---|---|
| Rapier | rapier | 400 × 10 | 1600 × 40 | left end, middle | The tip (right end) follows the drawn path. |
| Sword | sword | 320 × 15 | 1280 × 60 | left end, middle | The tip (right end) follows the drawn path. Design TBD in the GDD. |
| Mace | mace | 220 × 30 | 880 × 120 | left end, middle | The tip (right end) follows the drawn path. |
| Spear | spear | 500 × 8 | 2000 × 32 | left end, middle | The tip (right end) follows the drawn path. Design TBD in the GDD. |
| Shield | shield | 40 × 150 | 160 × 600 | centre | Held like a shield: its face looks right (+X); its centre follows the drawn path. Design TBD in the GDD. |
| Severed limb | severed_limb | 200 × 25 | 800 × 100 | left end, middle | The club: a dummy's own severed arm or leg, held by its torn end (GDD §12). Design TBD in the GDD. |
| Body blow | body_blow | none | none | none | Nothing is held: the head strikes (GDD §12). No art. Design TBD in the GDD. |

## Mechanical debris and effects

The game throws these from code (GDD §2: bolts, nuts, springs, gears, sparks and hydraulic fluid; never blood). Each is drawn **white on transparent** with soft edges and tinted by the game, pivot in the centre. Debris pieces tumble, bounce and stay on the floor; the rest fade. Sizes at the current tuning:

| Sprite | Drawn size (units) | Largest on screen (units) | Pixels (largest) | Shape |
|---|---|---|---|---|
| fx_bolt.png | 9 × 21 | the same | 36 × 84 | A hex-headed bolt, head up. |
| fx_nut.png | 11.7 × 11.7 | the same | 47 × 47 | A hex nut. |
| fx_spring.png | 12.6 × 29.4 | the same | 51 × 118 | A coil spring, upright. |
| fx_gear.png | 16.2 × 16.2 | the same | 65 × 65 | A small cog. |
| fx_drop.png | 7 × 9.6 | landed: 11.9 × 2.8 | 48 × 39 | A drop of hydraulic fluid, point up; it lands flattened into a puddle (the same sprite squashed). |
| fx_spark.png | 7 × 3.5 | 24.5 × 3.5 | 98 × 14 | A bright streak, stretched along its flight. |
| fx_puff.png | Ø 40 (dust), Ø 70 (steam) | Ø 175 | 700 × 700 | A soft cloud; it grows as it fades. |
| fx_ring.png | Ø 70 (partial block), Ø 120 (full block), Ø 160 (crush) | Ø 160 | 640 × 640 | A thin ring; it grows from 30% of its size as it fades. |
| fx_flash.png | Ø 60 (hit), Ø 90 (wall zap), Ø 140 (KO) | Ø 210 | 840 × 840 | A brief bright disc, soft-edged; it grows as it fades. |

The puff, ring and flash are soft shapes the game already draws well in code; art for them is optional.

## The electric wall

Each wall (GDD §13) is a vertical bar 14 units wide and 700 tall standing on the floor, with a glow 4 times as wide behind it. It springs up from the floor, pulses while it warns and while it is live, and the game tints it for each. Lightning arcs (3 units wide) crackle along it, drawn in code.

| File | Size (units) | Pixels | Notes |
|---|---|---|---|
| wall_bar.png | 14 × 700 | 56 × 2800 | White or light grey; it may repeat from top to bottom. |
| wall_glow.png | 56 × 700 | 224 × 2800 | A soft white glow, brightest in the middle, fading to both sides. |

## The arena background

One background (Session 17; the crash-test facility look is TBD in GDD §2). The arena is 2000 units wide, its edges at x = -1000 and x = 1000, the floor's top at y = 0. The camera usually shows 700 units of height centred between the dummies, at most 1150, never more than 60 past an edge (unless the screen is wider than the arena) and never more than 90 below the floor. So the background must cover x from -1300 to 1300 and y from -100 to 1150.

| File | Size (units) | Pixels | Notes |
|---|---|---|---|
| arena_background.png | 2600 × 1250 | 5200 × 2500 (at 2 px per unit) | Opaque. The floor's top surface runs 200 px above the bottom edge; the arena's edges are marked by the game on top. Keep strong detail away from the middle, where the dummies fight. |

## Not covered yet

- The HUD, menus and icons (Session 18).
- Skins (GDD §16; Session 17 adds the skin system): a skin changes how a piece looks, never its box.

## Delivery

- **Folder:** `Assets/XRim/Art/Incoming/` (create it if it is missing).
- **Format:** PNG, 32-bit with a transparent background, sRGB, at the *Pixels* sizes above (exactly twice that is fine for extra sharpness). Leave no empty margin around a piece: its edges are its size.
- **Names:** lower case, words joined by underscores, one file per piece (or one sprite sheet whose slices carry these names):

| File | What |
|---|---|
| dummy_torso.png | Torso |
| dummy_torso_bottom.png | TorsoBottom |
| dummy_torso_top.png | TorsoTop |
| dummy_head.png | Head |
| dummy_arm_upper.png | ArmUpper (both sides) |
| dummy_arm_lower.png | ArmLower (both sides) |
| dummy_leg_upper.png | LegUpper (both sides) |
| dummy_leg_lower.png | LegLower (both sides) |
| dummy_calibration_marker.png | The calibration-marker disc (optional: the game can draw its own) |
| weapon_rapier.png | Rapier |
| weapon_sword.png | Sword |
| weapon_mace.png | Mace |
| weapon_spear.png | Spear |
| weapon_shield.png | Shield |
| weapon_severed_limb.png | Severed limb |
| fx_bolt.png | The bolt effect |
| fx_nut.png | The nut effect |
| fx_spring.png | The spring effect |
| fx_gear.png | The gear effect |
| fx_drop.png | The drop effect |
| fx_spark.png | The spark effect |
| fx_puff.png | The puff effect |
| fx_ring.png | The ring effect |
| fx_flash.png | The flash effect |
| wall_bar.png, wall_glow.png | The electric wall |
| arena_background.png | The arena background |

- **Left and right limbs** share one drawing; the far one draws behind the torso. If they should differ, add the side: `dummy_arm_upper_left.png`, `dummy_arm_upper_right.png`.

