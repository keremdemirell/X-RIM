using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Presentation.Arena;
using XRim.Presentation.Vfx;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Presentation.ArtSpec
{
    /// <summary>
    /// Writes Docs/ART_SPEC.md (Session 13): every piece of art the game draws for the dummies, their held items, the mechanical debris and
    /// the electric wall, with sizes in arena units and suggested pixels, pivots, joints, draw order, calibration-marker zones, and how to
    /// name and deliver the files. The dummy comes from <see cref="DummyLayout"/> (what the ragdoll builder builds), held items from
    /// <see cref="HeldItemShape"/> (what the physics uses), effects from the effect code's own sizes. Pure: the same inputs give the same text.
    /// </summary>
    public static class ArtSpecDocument
    {
        /// <summary>The density to draw at: a 1080-pixel-tall phone shows about 1.5 px per arena unit, about 2.4 while the replay zooms in.</summary>
        [Placeholder("Session 13: 4 px per arena unit leaves room above the replay zoom on sharper screens; confirm with the first art")]
        public const float PixelsPerArenaUnit = 4f;

        /// <summary>The background sits behind the action and is seen softer: half the density is plenty.</summary>
        [Placeholder("Session 13: half the pieces' density for the arena background; confirm with the first art")]
        public const float BackgroundPixelsPerArenaUnit = 2f;

        /// <summary>The widest screen the background must fill (a 20:9 phone held sideways).</summary>
        [Placeholder("Session 13: 20:9 is the widest phone shape laid out so far (Session 08)")]
        public const float WidestScreenAspect = 20f / 9f;

        public const string DeliveryFolder = "Assets/XRim/Art/Incoming/";

        /// <summary>The screen height the pixel advice is worked out for (a common phone held sideways).</summary>
        private const float ReferenceScreenHeightPixels = 1080f;

        /// <summary>The background's edges are rounded out to this many arena units.</summary>
        private const float BackgroundRoundingUnits = 50f;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Write(ArtSpecInputs inputs)
        {
            Guard.NotNull(inputs, nameof(inputs));
            var text = new StringBuilder();
            DummyLayout layout = DummyLayout.Of(inputs.Simulation.Ragdoll, inputs.Rules.Paths, inputs.Simulation.Segmentation);
            WriteHeader(text, inputs);
            WriteGroundRules(text, inputs);
            WriteDummy(text, inputs, layout);
            WriteHeldItems(text, inputs, layout);
            WriteEffects(text, inputs);
            WriteWall(text);
            WriteBackground(text, inputs);
            WriteNotCovered(text);
            WriteDelivery(text, inputs);
            return text.ToString();
        }

        /// <summary>A file name for a piece of dummy art: lower case, words joined by underscores (TorsoBottom → dummy_torso_bottom).</summary>
        public static string DummyFileName(string pieceName)
        {
            Guard.NotNull(pieceName, nameof(pieceName));
            string name = pieceName.Replace("Left", string.Empty).Replace("Right", string.Empty);
            return "dummy_" + Snake(name) + ".png";
        }

        public static string WeaponFileName(WeaponStats weapon) => "weapon_" + Guard.NotNull(weapon, nameof(weapon)).Id + ".png";

        public static string EffectFileName(VfxParticleKind kind) => "fx_" + kind.ToString().ToLowerInvariant() + ".png";

        private static void WriteHeader(StringBuilder text, ArtSpecInputs inputs)
        {
            text.AppendLine("# X-RIM art spec");
            text.AppendLine();
            string date = string.IsNullOrEmpty(inputs.Date) ? string.Empty : " on " + inputs.Date;
            text.AppendLine($"*Generated from {inputs.Source}{date}. To regenerate it from your tuning, run the Unity menu " +
                            "**XRim/Reports/Write Art Spec**; do not edit it by hand.*");
            text.AppendLine();
            text.AppendLine("Everything the game draws for the dummies, their weapons, the mechanical debris and the electric wall: sizes, pivots, " +
                            "joints, draw order, and how to name and deliver the files. The art replaces the placeholder shapes in Session 17.");
            text.AppendLine();
        }

        private static void WriteGroundRules(StringBuilder text, ArtSpecInputs inputs)
        {
            float closeView = inputs.DuelViewHeightUnits * inputs.ReplayZoomViewFraction;
            text.AppendLine("## Ground rules");
            text.AppendLine();
            text.AppendLine($"- **Units.** Sizes are in *arena units*, the game's own measure (the dummy's torso is {Size(inputs.Simulation.Ragdoll.TorsoWidthUnits, inputs.Simulation.Ragdoll.TorsoHeightUnits)}). " +
                            $"In Unity, 1 arena unit is {Number(inputs.WorldUnitsPerArenaUnit, "0.####")} world units.");
            text.AppendLine($"- **Pixels.** Draw at **{Number(PixelsPerArenaUnit)} px per arena unit**. The duel camera shows about {Number(inputs.DuelViewHeightUnits)} " +
                            $"units of height, so a {Number(ReferenceScreenHeightPixels)}-pixel-tall phone shows {Number(ReferenceScreenHeightPixels / inputs.DuelViewHeightUnits, "0.0")} px " +
                            $"per unit, and {Number(ReferenceScreenHeightPixels / closeView, "0.0")} when the instant replay closes in; " +
                            $"{Number(PixelsPerArenaUnit)} leaves room for sharper screens. The *Pixels* columns are sizes at that density. Import the " +
                            $"sprites with **Pixels Per Unit = {Number(PixelsPerArenaUnit / inputs.WorldUnitsPerArenaUnit)}**.");
            text.AppendLine("- **Facing.** Draw everything facing **right** (+X), standing upright. The right-hand dummy is the same art mirrored.");
            text.AppendLine("- **Pivots.** Every piece turns about a joint; put its pivot exactly there (given below as a point on the sprite).");
            text.AppendLine("- **Draw order.** Far-side limbs (the dummy's left arm and leg) behind the torso, the head over the torso, near-side limbs " +
                            "(its right arm and leg) in front, the held weapon over everything. Higher numbers draw on top.");
            text.AppendLine("- **No blood, ever** (GDD §2). Damage is bolts, nuts, springs, gears, sparks and hydraulic fluid.");
            text.AppendLine($"- **Calibration markers.** Paint the yellow-and-black quartered disc of a crash-test dummy into the middle of each body " +
                            $"piece, {Number(DummyLayout.MarkerSizeFraction * 100f)}% of the piece's narrower side across (the *Marker* column): " +
                            "the markers show the hit zones, so pieces of one zone share a marker size. The placeholder draws them in the same spots.");
            text.AppendLine();
        }

        private static void WriteDummy(StringBuilder text, ArtSpecInputs inputs, DummyLayout layout)
        {
            int bodies = (int)layout.Segmentation;
            text.AppendLine($"## The dummy: {bodies} bodies");
            text.AppendLine();
            text.AppendLine($"The current setting builds the dummy from {bodies} bodies (D2){(layout.Segmentation == RagdollSegmentation.TenBodies ? ": each arm and leg in two pieces" : ": one piece per arm and leg")}. " +
                            "Points are measured from the **pelvis** at (0, 0), standing, facing right, y up.");
            text.AppendLine();
            text.AppendLine("| Piece | Hit zone | Size (units) | Pixels | Pivot on the sprite | Pivot on the dummy | Draw order | Marker Ø (units) |");
            text.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (DummyBodyLayout body in layout.Bodies)
            {
                string zone = body.OnlyAfterDeath ? body.Part + " (after a KO)" : body.Part.ToString();
                string size = body.IsRound ? "Ø " + Number(body.SizeUnits.X) : Size(body.SizeUnits.X, body.SizeUnits.Y);
                text.AppendLine($"| {body.Name} | {zone} | {size} | {Pixels(body.SizeUnits.X, body.SizeUnits.Y)} | {PivotOnSprite(body)} | " +
                                $"{Point(body.PivotUnits)} | {body.SortingOrder} | {Number(body.MarkerDiameterUnits)} |");
            }

            text.AppendLine();
            text.AppendLine("### Joints");
            text.AppendLine();
            text.AppendLine("Each joint is the child piece's pivot. Angles are measured from the standing pose; a positive angle turns the child " +
                            "counter-clockwise (a hanging arm or leg swings forward).");
            text.AppendLine();
            text.AppendLine("| Joint | Child piece | Parent piece | Point on the dummy | Turns (degrees) |");
            text.AppendLine("|---|---|---|---|---|");
            foreach (DummyJointLayout joint in layout.Joints)
            {
                text.AppendLine($"| {joint.Name} | {joint.ChildBody} | {joint.ParentBody} | {Point(joint.AnchorUnits)} | " +
                                $"{Number(joint.MinDegrees)} to {Number(joint.MaxDegrees)} |");
            }

            DummyBodyLayout top = layout.Find(DummyLayout.TorsoTopName);
            text.AppendLine();
            text.AppendLine("### When a dummy is knocked out");
            text.AppendLine();
            text.AppendLine($"At a KO the torso splits across its middle (GDD §11), {Number(DummyLayout.TorsoHalfFraction * 100f)}% of the way up, at " +
                            $"y = {Number(top.PivotUnits.Y)}: **TorsoBottom** and **TorsoTop** replace the torso. Draw both halves with torn edges " +
                            "that meet at that line.");
            text.AppendLine();
            text.AppendLine("### Loose limbs");
            text.AppendLine();
            text.AppendLine("A severed arm or leg (GDD §12) keeps its pieces: it lies on the floor drawn with the same art, pivoting at the same end. " +
                            "Its torn end (the shoulder or the hip) then shows, so draw that end as a socket or torn metal. A severed limb picked up " +
                            "and swung is drawn as the held club (below).");
            text.AppendLine();

            RagdollSegmentation other = layout.Segmentation == RagdollSegmentation.TenBodies ? RagdollSegmentation.SixBodies : RagdollSegmentation.TenBodies;
            DummyLayout otherLayout = DummyLayout.Of(inputs.Simulation.Ragdoll, inputs.Rules.Paths, other);
            text.AppendLine($"### If the dummy is built from {(int)other} bodies instead (D2)");
            text.AppendLine();
            var limbs = new List<string>();
            foreach (DummyBodyLayout body in otherLayout.Bodies)
            {
                if (body.Part.IsArm() || body.Part.IsLeg())
                    limbs.Add($"{body.Name} {Size(body.SizeUnits.X, body.SizeUnits.Y)} (pivot {PivotOnSprite(body)}, at {Point(body.PivotUnits)})");
            }

            text.AppendLine((other == RagdollSegmentation.SixBodies
                                ? "Each arm and leg is one piece: "
                                : "Each arm and leg splits into an upper and a lower piece at the elbow or knee: ") + string.Join("; ", limbs) + ". " +
                            "Drawing each limb as one picture split at the elbow or knee serves both.");
            text.AppendLine();
        }

        private static void WriteHeldItems(StringBuilder text, ArtSpecInputs inputs, DummyLayout layout)
        {
            text.AppendLine("## Weapons and held items");
            text.AppendLine();
            text.AppendLine($"A held item is drawn in its own frame: the **grip** at the origin, pointing right (+X). At rest the grip sits at the end of " +
                            $"the hanging right arm, {Point(layout.RestGripUnits)} from the pelvis. Held items draw over everything (order " +
                            $"{DummyLayout.HeldItemOrder}). Keep each drawing inside its box: the box is exactly what can hit (GDD §6: the weapon's " +
                            "length and hit width).");
            text.AppendLine();
            text.AppendLine("| Item | Id | Size along × across (units) | Pixels | Grip (pivot on the sprite) | Notes |");
            text.AppendLine("|---|---|---|---|---|---|");
            foreach (WeaponStats weapon in inputs.Rules.Weapons)
            {
                string tbd = weapon.DesignIsTbd ? " Design TBD in the GDD." : string.Empty;
                if (weapon.Kind == WeaponKind.BodyBlow)
                {
                    text.AppendLine($"| {weapon.DisplayName} | {weapon.Id} | none | none | none | Nothing is held: the head strikes (GDD §12). No art.{tbd} |");
                    continue;
                }

                HeldItemShape shape = HeldItemShape.Of(weapon);
                string grip;
                string note;
                switch (weapon.Kind)
                {
                    case WeaponKind.Shield:
                        grip = "centre";
                        note = "Held like a shield: its face looks right (+X); its centre follows the drawn path.";
                        break;
                    case WeaponKind.SeveredLimb:
                        grip = "left end, middle";
                        note = "The club: a dummy's own severed arm or leg, held by its torn end (GDD §12).";
                        break;
                    default:
                        grip = "left end, middle";
                        note = "The tip (right end) follows the drawn path.";
                        break;
                }

                text.AppendLine($"| {weapon.DisplayName} | {weapon.Id} | {Size(shape.SizeUnits.X, shape.SizeUnits.Y)} | " +
                                $"{Pixels(shape.SizeUnits.X, shape.SizeUnits.Y)} | {grip} | {note}{tbd} |");
            }

            text.AppendLine();
        }

        private static void WriteEffects(StringBuilder text, ArtSpecInputs inputs)
        {
            float debris = inputs.DebrisSizeUnits;
            float drop = inputs.FluidDropSizeUnits;
            float spark = inputs.SparkSizeUnits;
            text.AppendLine("## Mechanical debris and effects");
            text.AppendLine();
            text.AppendLine("The game throws these from code (GDD §2: bolts, nuts, springs, gears, sparks and hydraulic fluid; never blood). Each " +
                            "is drawn **white on transparent** with soft edges and tinted by the game, pivot in the centre. Debris pieces tumble, " +
                            "bounce and stay on the floor; the rest fade. Sizes at the current tuning:");
            text.AppendLine();
            text.AppendLine("| Sprite | Drawn size (units) | Largest on screen (units) | Pixels (largest) | Shape |");
            text.AppendLine("|---|---|---|---|---|");
            EffectRow(text, VfxParticleKind.Bolt, debris, debris * Aspect(VfxSprites.BoltWidth, VfxSprites.BoltHeight), null, "A hex-headed bolt, head up.");
            float nut = debris * VfxRecipes.NutSizeMultiplier;
            EffectRow(text, VfxParticleKind.Nut, nut, nut, null, "A hex nut.");
            float spring = debris * VfxRecipes.SpringSizeMultiplier;
            EffectRow(text, VfxParticleKind.Spring, spring, spring * Aspect(VfxSprites.SpringWidth, VfxSprites.SpringHeight), null, "A coil spring, upright.");
            float gear = debris * VfxRecipes.GearSizeMultiplier;
            EffectRow(text, VfxParticleKind.Gear, gear, gear, null, "A small cog.");
            float dropHeight = drop * Aspect(VfxSprites.DropWidth, VfxSprites.DropHeight);
            text.AppendLine($"| {EffectFileName(VfxParticleKind.Drop)} | {Size(drop, dropHeight)} | landed: {Size(drop * MatchVfx.PuddleWidthMultiplier, drop * MatchVfx.PuddleHeightMultiplier)} | " +
                            $"{Pixels(Math.Max(drop, drop * MatchVfx.PuddleWidthMultiplier), dropHeight)} | A drop of hydraulic fluid, point up; it lands " +
                            "flattened into a puddle (the same sprite squashed). |");
            text.AppendLine($"| {EffectFileName(VfxParticleKind.Spark)} | {Size(spark, spark * MatchVfx.SparkThicknessShare)} | " +
                            $"{Size(spark * MatchVfx.SparkStretch, spark * MatchVfx.SparkThicknessShare)} | " +
                            $"{Pixels(spark * MatchVfx.SparkStretch, spark * MatchVfx.SparkThicknessShare)} | A bright streak, stretched along its flight. |");
            text.AppendLine($"| {EffectFileName(VfxParticleKind.Puff)} | Ø {Number(VfxRecipes.DustPuffUnits)} (dust), Ø {Number(VfxRecipes.SteamPuffUnits)} (steam) | " +
                            $"Ø {Number(VfxRecipes.SteamPuffUnits * (1f + MatchVfx.PuffGrowth))} | {Pixels(VfxRecipes.SteamPuffUnits * (1f + MatchVfx.PuffGrowth), VfxRecipes.SteamPuffUnits * (1f + MatchVfx.PuffGrowth))} | " +
                            "A soft cloud; it grows as it fades. |");
            text.AppendLine($"| {EffectFileName(VfxParticleKind.Ring)} | Ø {Number(VfxRecipes.PartialBlockRingUnits)} (partial block), Ø {Number(VfxRecipes.FullBlockRingUnits)} (full block), " +
                            $"Ø {Number(VfxRecipes.CrushRingUnits)} (crush) | Ø {Number(VfxRecipes.CrushRingUnits)} | {Pixels(VfxRecipes.CrushRingUnits, VfxRecipes.CrushRingUnits)} | " +
                            $"A thin ring; it grows from {Number(MatchVfx.RingStartShare * 100f)}% of its size as it fades. |");
            float biggestFlash = Math.Max(VfxRecipes.BigFlashSizeUnits, VfxRecipes.ZapFlashSizeUnits) * (1f + MatchVfx.FlashGrowth);
            text.AppendLine($"| {EffectFileName(VfxParticleKind.Flash)} | Ø {Number(VfxRecipes.FlashSizeUnits)} (hit), Ø {Number(VfxRecipes.ZapFlashSizeUnits)} (wall zap), " +
                            $"Ø {Number(VfxRecipes.BigFlashSizeUnits)} (KO) | Ø {Number(biggestFlash)} | {Pixels(biggestFlash, biggestFlash)} | " +
                            "A brief bright disc, soft-edged; it grows as it fades. |");
            text.AppendLine();
            text.AppendLine("The puff, ring and flash are soft shapes the game already draws well in code; art for them is optional.");
            text.AppendLine();
        }

        private static void EffectRow(StringBuilder text, VfxParticleKind kind, float width, float height, string largest, string shape) =>
            text.AppendLine($"| {EffectFileName(kind)} | {Size(width, height)} | {largest ?? "the same"} | {Pixels(width, height)} | {shape} |");

        private static void WriteWall(StringBuilder text)
        {
            float glow = ElectricWallView.BarWidthUnits * ElectricWallView.GlowWidthMultiplier;
            text.AppendLine("## The electric wall");
            text.AppendLine();
            text.AppendLine($"Each wall (GDD §13) is a vertical bar {Number(ElectricWallView.BarWidthUnits)} units wide and " +
                            $"{Number(ElectricWallView.BarHeightUnits)} tall standing on the floor, with a glow {Number(ElectricWallView.GlowWidthMultiplier)} " +
                            $"times as wide behind it. It springs up from the floor, pulses while it warns and while it is live, and the game tints it " +
                            $"for each. Lightning arcs ({Number(ElectricWallView.ArcWidthUnits)} units wide) crackle along it, drawn in code.");
            text.AppendLine();
            text.AppendLine("| File | Size (units) | Pixels | Notes |");
            text.AppendLine("|---|---|---|---|");
            text.AppendLine($"| wall_bar.png | {Size(ElectricWallView.BarWidthUnits, ElectricWallView.BarHeightUnits)} | " +
                            $"{Pixels(ElectricWallView.BarWidthUnits, ElectricWallView.BarHeightUnits)} | White or light grey; it may repeat from top to bottom. |");
            text.AppendLine($"| wall_glow.png | {Size(glow, ElectricWallView.BarHeightUnits)} | {Pixels(glow, ElectricWallView.BarHeightUnits)} | " +
                            "A soft white glow, brightest in the middle, fading to both sides. |");
            text.AppendLine();
        }

        /// <summary>The area the camera can ever show: the whole arena and its margins, as wide as the widest phone at the most height.</summary>
        private static void WriteBackground(StringBuilder text, ArtSpecInputs inputs)
        {
            ArenaEdges edges = new RulePolicies().ArenaEdge.EdgesFor(inputs.Rules.Arena);
            float halfWidth = Math.Max(edges.WidthUnits * 0.5f + inputs.FramingEdgeMarginUnits, inputs.MaxViewHeightUnits * WidestScreenAspect * 0.5f);
            float left = edges.CentreXUnits - RoundUp(halfWidth);
            float right = edges.CentreXUnits + RoundUp(halfWidth);
            float bottom = -RoundUp(inputs.FramingFloorMarginUnits);
            float top = RoundUp(inputs.MaxViewHeightUnits);
            float floorPixels = -bottom * BackgroundPixelsPerArenaUnit;
            text.AppendLine("## The arena background");
            text.AppendLine();
            text.AppendLine($"One background (Session 17; the crash-test facility look is TBD in GDD §2). The arena is {Number(edges.WidthUnits)} units " +
                            $"wide, its edges at x = {Number(edges.LeftXUnits)} and x = {Number(edges.RightXUnits)}, the floor's top at y = 0. The camera " +
                            $"usually shows {Number(inputs.DuelViewHeightUnits)} units of height centred between the dummies, at most " +
                            $"{Number(inputs.MaxViewHeightUnits)}, never more than {Number(inputs.FramingEdgeMarginUnits)} past an edge (unless the " +
                            $"screen is wider than the arena) and never more than {Number(inputs.FramingFloorMarginUnits)} below the floor. So the " +
                            $"background must cover x from {Number(left)} to {Number(right)} and y from {Number(bottom)} to {Number(top)}.");
            text.AppendLine();
            text.AppendLine("| File | Size (units) | Pixels | Notes |");
            text.AppendLine("|---|---|---|---|");
            text.AppendLine($"| arena_background.png | {Size(right - left, top - bottom)} | {Size((right - left) * BackgroundPixelsPerArenaUnit, (top - bottom) * BackgroundPixelsPerArenaUnit)} " +
                            $"(at {Number(BackgroundPixelsPerArenaUnit)} px per unit) | Opaque. The floor's top surface runs {Number(floorPixels)} px above the " +
                            "bottom edge; the arena's edges are marked by the game on top. Keep strong detail away from the middle, where the dummies fight. |");
            text.AppendLine();
        }

        private static void WriteNotCovered(StringBuilder text)
        {
            text.AppendLine("## Not covered yet");
            text.AppendLine();
            text.AppendLine("- The HUD, menus and icons (Session 18).");
            text.AppendLine("- Skins (GDD §16; Session 17 adds the skin system): a skin changes how a piece looks, never its box.");
            text.AppendLine();
        }

        private static void WriteDelivery(StringBuilder text, ArtSpecInputs inputs)
        {
            text.AppendLine("## Delivery");
            text.AppendLine();
            text.AppendLine($"- **Folder:** `{DeliveryFolder}` (create it if it is missing).");
            text.AppendLine("- **Format:** PNG, 32-bit with a transparent background, sRGB, at the *Pixels* sizes above (exactly twice that is fine " +
                            "for extra sharpness). Leave no empty margin around a piece: its edges are its size.");
            text.AppendLine("- **Names:** lower case, words joined by underscores, one file per piece (or one sprite sheet whose slices carry these names):");
            text.AppendLine();
            text.AppendLine("| File | What |");
            text.AppendLine("|---|---|");
            var seen = new HashSet<string>();
            DummyLayout layout = DummyLayout.Of(inputs.Simulation.Ragdoll, inputs.Rules.Paths, inputs.Simulation.Segmentation);
            foreach (DummyBodyLayout body in layout.Bodies)
            {
                string file = DummyFileName(body.Name);
                if (!seen.Add(file)) continue;
                string what = body.Part.IsArm() || body.Part.IsLeg() ? $"{body.Name.Replace("Left", string.Empty)} (both sides)" : body.Name;
                text.AppendLine($"| {file} | {what} |");
            }

            text.AppendLine("| dummy_calibration_marker.png | The calibration-marker disc (optional: the game can draw its own) |");
            foreach (WeaponStats weapon in inputs.Rules.Weapons)
            {
                if (weapon.Kind != WeaponKind.BodyBlow) text.AppendLine($"| {WeaponFileName(weapon)} | {weapon.DisplayName} |");
            }

            foreach (VfxParticleKind kind in (VfxParticleKind[])Enum.GetValues(typeof(VfxParticleKind)))
            {
                text.AppendLine($"| {EffectFileName(kind)} | The {kind.ToString().ToLowerInvariant()} effect |");
            }

            text.AppendLine("| wall_bar.png, wall_glow.png | The electric wall |");
            text.AppendLine("| arena_background.png | The arena background |");
            text.AppendLine();
            text.AppendLine("- **Left and right limbs** share one drawing; the far one draws behind the torso. If they should differ, add the side: " +
                            "`dummy_arm_upper_left.png`, `dummy_arm_upper_right.png`.");
            text.AppendLine();
        }

        private static string PivotOnSprite(DummyBodyLayout body)
        {
            if (body.IsRound) return "bottom middle (the neck)";
            return body.Rises ? "bottom middle" : "top middle";
        }

        private static float Aspect(int width, int height) => (float)height / width;

        private static float RoundUp(float units) => (float)Math.Ceiling(units / BackgroundRoundingUnits - 1e-4) * BackgroundRoundingUnits;

        private static string Snake(string pascal)
        {
            var name = new StringBuilder();
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (char.IsUpper(c) && i > 0) name.Append('_');
                name.Append(char.ToLowerInvariant(c));
            }

            return name.ToString();
        }

        private static string Number(float value, string format = "0.#") => value.ToString(format, Invariant);

        private static string Size(float width, float height) => Number(width) + " × " + Number(height);

        private static string Point(Vec2 point) => $"({Number(point.X)}, {Number(point.Y)})";

        private static string Pixels(float widthUnits, float heightUnits) =>
            Math.Ceiling(widthUnits * PixelsPerArenaUnit - 1e-3).ToString(Invariant) + " × " +
            Math.Ceiling(heightUnits * PixelsPerArenaUnit - 1e-3).ToString(Invariant);
    }
}
