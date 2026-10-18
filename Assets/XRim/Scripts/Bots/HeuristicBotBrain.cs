using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Bots
{
    /// <summary>
    /// A practice and development opponent that reads the board and tries to land hits (Session 16; GDD §17: practice and development
    /// only). It sees only what <see cref="BotContext"/> shows (public information) and plans the moment planning opens:
    /// <list type="number">
    /// <item><b>Weapon</b>: a weighted pick among the weapons it may hold, by base damage × its share of the race to arrive first (its speed
    /// against the opponent's, §9) × whether it reaches this turn (as it stands, or with a lunge); a shield only to defend when threatened and
    /// low on HP.</item>
    /// <item><b>Body move</b>: a lunge when only a lunge reaches; a duck under a heavier weapon that can reach it, with a low thrust (the GDD
    /// §1 moment); a step back when threatened and low on HP, never with its own wall standing behind it (§13); a lunge to close in when
    /// nothing reaches at all (its ground carries over, §3); now and then a jump.</item>
    /// <item><b>Target</b>: a damaged limb it can reach (hunting a sever, §11), else sometimes the head, else the torso (just above the pelvis
    /// when ducking), missed by up to <see cref="HeuristicBotSettings.AimErrorUnits"/>; the drawing allows for its own body move.</item>
    /// <item><b>Attack</b>: a usable signature move when the opponent is within its pattern (§8), else a thrust or a swing (a rigid weapon
    /// only thrusts straight), a shield raised in front of the chest.</item>
    /// </list>
    /// Every stroke starts at the weapon tip (no lead-in ink, D3), stays inside the reach limit and within the ink budget, so every plan is
    /// one the rules accept. All its choices come from the context's seeded random; some turns (<see cref="HeuristicBotSettings.MistakeChance"/>)
    /// it plays like the random bot.
    /// </summary>
    public sealed class HeuristicBotBrain : IBotBrain
    {
        /// <summary>Every point stays this far inside the reach circle, so float rounding never clamps it (technical).</summary>
        private const float ReachSafetyUnits = 2f;

        /// <summary>The stroke uses at most this share of the ink, so rounding never cuts it (technical).</summary>
        private const float InkSafetyShare = 0.95f;

        /// <summary>A target counts as in reach this far inside the reach limit (bot behaviour).</summary>
        private const float ReachMarginUnits = 20f;

        /// <summary>A thrust comes in along the line from the shoulder, from this far before its target, and carries on this far past it.</summary>
        private const float ThrustApproachUnits = 100f;
        private const float ThrustThroughUnits = 40f;

        /// <summary>A swing is a chop: it starts back and above its target and follows through forward and down (bot behaviour).</summary>
        private static readonly Vec2 SwingStartFromTarget = new Vec2(-60f, 140f);
        private static readonly Vec2 SwingFollowThroughFromTarget = new Vec2(30f, -70f);

        /// <summary>
        /// Where a raised shield stands, face out in front of the chest: where the arc ClashPlayModeTests proves blocks a thrust ends. The bot
        /// raises it there in one straight move, about 100 units from its rest, inside the shield's short ink (150).
        /// </summary>
        private static readonly Vec2 ShieldRaisedLocal = new Vec2(110f, 100f);

        /// <summary>The head's centre sits above the neck by this share of the pelvis-to-neck span (25 of 120 on the placeholder dummy).</summary>
        private const float HeadAboveNeckShareOfTorso = 0.2f;

        /// <summary>A ducking thrust aims this share of the way from the pelvis up the torso: just above the pelvis.</summary>
        private const float LowTargetShareOfTorso = 0.25f;

        /// <summary>A signature move's pattern counts as reaching when one of its points comes this close to the opponent's torso or head.</summary>
        private const float SignatureNearUnits = 60f;

        /// <summary>The shortest stroke the bot sends: the rules turn a shorter one into an empty path.</summary>
        private const float MinimumStrokeUnits = 10f;

        /// <summary>How much a weapon that reaches only with a lunge, or not at all, counts against one that reaches as it stands.</summary>
        private const float ReachesWithLungeWeight = 0.6f;
        private const float ReachesNotWeight = 0.15f;

        /// <summary>A limb's long axis points down (−90°) in the rest pose (GuardStance), so its direction is its rotation less this.</summary>
        private const float LimbAxisOffsetDegrees = 90f;

        /// <summary>On a six-body dummy a limb is struck this share of an arm's length down from its joint.</summary>
        private const float LimbStrikeShareOfArm = 0.5f;

        private static readonly BodyPart[] Limbs = { BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };

        private readonly HeuristicBotSettings _settings;
        private readonly RandomBotBrain _mistakes = new RandomBotBrain(0f);

        public HeuristicBotBrain(HeuristicBotSettings settings = null)
        {
            _settings = settings ?? new HeuristicBotSettings();
        }

        public TurnPlan PlanTurn(BotContext context)
        {
            Guard.NotNull(context, nameof(context));
            if (Chance(context, _settings.MistakeChance)) return _mistakes.PlanTurn(context);

            var board = new Board(context);
            WeaponId weapon = ChooseWeapon(context, board);
            WeaponStats stats = Stats(context, weapon);
            BodyMove move = ChooseBodyMove(context, board, stats);
            SignatureMoveStats signature = ChooseSignature(context, board, weapon, ref move);
            if (signature != null) return new TurnPlan(weapon, move, WeaponPath.Empty, signature.MoveId, true);

            Vec2 target = ChooseTarget(context, board, stats, move);
            return new TurnPlan(weapon, move, DrawAttack(context, stats, move, target), default, true);
        }

        // --- Weapon ------------------------------------------------------------------------------

        private WeaponId ChooseWeapon(BotContext context, Board board)
        {
            WeaponId current = context.CurrentWeapon;
            PlanningConstraints constraints = context.Constraints;
            if (!constraints.CanSwitchWeapon) return current;
            List<WeaponId> options = constraints.AvailableWeapons(context.Loadout);
            if (options.Count == 0) return current;

            if (board.Threatened && IsLowOnHp(context) && Chance(context, _settings.ShieldGuardChance))
            {
                WeaponId shield = options.Find(id => Stats(context, id).Kind == WeaponKind.Shield);
                if (!shield.IsEmpty) return shield;
            }

            var weights = new float[options.Count];
            float total = 0f;
            for (int i = 0; i < options.Count; i++)
            {
                weights[i] = AttackWeight(context, board, Stats(context, options[i]));
                total += weights[i];
            }

            if (total <= 0f) return options.Contains(current) ? current : options[0];
            float pick = context.Random.NextFloat() * total;
            for (int i = 0; i < options.Count; i++)
            {
                pick -= weights[i];
                if (pick < 0f) return options[i];
            }

            return options[options.Count - 1];
        }

        /// <summary>Base damage × the share of the race to arrive first (§9) × whether it reaches the torso this turn; 0 for a shield.</summary>
        private static float AttackWeight(BotContext context, Board board, WeaponStats weapon)
        {
            if (weapon.Kind == WeaponKind.Shield) return 0f;
            float opponentSpeed = board.OpponentWeapon != null ? board.OpponentWeapon.SpeedUnitsPerSecond : weapon.SpeedUnitsPerSecond;
            float raceShare = weapon.SpeedUnitsPerSecond / Math.Max(weapon.SpeedUnitsPerSecond + opponentSpeed, 1f);
            float reach = Reaches(context, weapon, board.OpponentTorso, BodyMove.None) ? 1f
                : context.Constraints.IsBodyMoveAllowed(BodyMove.Lunge) && Reaches(context, weapon, board.OpponentTorso, BodyMove.Lunge) ? ReachesWithLungeWeight
                : ReachesNotWeight;
            return Math.Max(weapon.BaseDamage, 0f) * raceShare * reach;
        }

        // --- Body move ---------------------------------------------------------------------------

        private BodyMove ChooseBodyMove(BotContext context, Board board, WeaponStats weapon)
        {
            PlanningConstraints constraints = context.Constraints;
            bool canStepBack = constraints.IsBodyMoveAllowed(BodyMove.StepBack) && !context.Self.WallIsUp;
            if (weapon.Kind == WeaponKind.Shield)
                return board.Threatened && IsLowOnHp(context) && canStepBack && Chance(context, _settings.RetreatChance) ? BodyMove.StepBack : BodyMove.None;

            if (!Reaches(context, weapon, board.OpponentTorso, BodyMove.None) && constraints.IsBodyMoveAllowed(BodyMove.Lunge) &&
                Reaches(context, weapon, board.OpponentTorso, BodyMove.Lunge))
                return BodyMove.Lunge;

            if (board.Threatened && board.OpponentWeapon != null && board.OpponentWeapon.Mass >= weapon.Mass &&
                constraints.IsBodyMoveAllowed(BodyMove.Crouch) && Reaches(context, weapon, board.OpponentLowTorso, BodyMove.Crouch) &&
                Chance(context, _settings.DuckChance))
                return BodyMove.Crouch;

            if (board.Threatened && IsLowOnHp(context) && canStepBack && Chance(context, _settings.RetreatChance)) return BodyMove.StepBack;

            // Nothing reaches even with a lunge (a short weapon at the starting gap): lunge anyway to close in, since a lunge's ground carries
            // over (§3). Standing still would leave both waiting for the turn cap (the first batch, 2026-10-18).
            if (!Reaches(context, weapon, board.OpponentTorso, BodyMove.Lunge) && constraints.IsBodyMoveAllowed(BodyMove.Lunge)) return BodyMove.Lunge;

            if (board.Threatened && constraints.IsBodyMoveAllowed(BodyMove.Jump) && Chance(context, _settings.JumpChance)) return BodyMove.Jump;
            return BodyMove.None;
        }

        // --- Signature move ----------------------------------------------------------------------

        /// <summary>A usable move whose pattern comes near the opponent's torso or head (§8), sometimes; it may bring its own body move.</summary>
        private SignatureMoveStats ChooseSignature(BotContext context, Board board, WeaponId weapon, ref BodyMove move)
        {
            if (!context.Signatures.CanUse) return null;
            var usable = new List<SignatureMoveStats>();
            foreach (SignatureMoveStats candidate in context.Signatures.Moves)
            {
                if (!candidate.AllowsWeapon(weapon)) continue;
                if (candidate.OverridesBodyMove && !context.Constraints.IsBodyMoveAllowed(candidate.BodyMoveOverride)) continue;
                BodyMove itsMove = candidate.OverridesBodyMove ? candidate.BodyMoveOverride : move;
                if (PatternReaches(context, board, candidate, Stats(context, weapon), itsMove)) usable.Add(candidate);
            }

            if (usable.Count == 0 || !Chance(context, _settings.SignatureChance)) return null;
            SignatureMoveStats chosen = usable[context.Random.NextInt(0, usable.Count)];
            if (chosen.OverridesBodyMove) move = chosen.BodyMoveOverride;
            return chosen;
        }

        private static bool PatternReaches(BotContext context, Board board, SignatureMoveStats move, WeaponStats weapon, BodyMove bodyMove)
        {
            Vec2 shift = Shift(context, bodyMove);
            ReachLimit reach = ReachLimit.For(context.Settings.Paths, weapon);
            foreach (Vec2 point in move.PresetPath)
            {
                Vec2 reached = Inside(point, reach.OriginUnits, reach.RadiusUnits) + shift;
                if (Vec2.Distance(reached, board.OpponentTorso) <= SignatureNearUnits || Vec2.Distance(reached, board.OpponentHead) <= SignatureNearUnits)
                    return true;
            }

            return false;
        }

        // --- Target ------------------------------------------------------------------------------

        private Vec2 ChooseTarget(BotContext context, Board board, WeaponStats weapon, BodyMove move)
        {
            Vec2 target;
            if (weapon.Kind == WeaponKind.Shield) target = ShieldRaisedLocal;
            else if (TryDamagedLimb(context, board, weapon, move, out Vec2 limb)) target = limb;
            else if (move == BodyMove.Crouch) target = board.OpponentLowTorso;
            else if (Reaches(context, weapon, board.OpponentHead, move) && Chance(context, _settings.HeadAimChance)) target = board.OpponentHead;
            else target = board.OpponentTorso;

            if (weapon.Kind == WeaponKind.Shield) return target;
            IRandom random = context.Random;
            float error = Math.Max(_settings.AimErrorUnits, 0f);
            return target + new Vec2((random.NextFloat() * 2f - 1f) * error, (random.NextFloat() * 2f - 1f) * error);
        }

        /// <summary>The most damaged limb at or over the focus share of its durability that the weapon reaches (§11: hunting a sever).</summary>
        private bool TryDamagedLimb(BotContext context, Board board, WeaponStats weapon, BodyMove move, out Vec2 point)
        {
            point = default;
            float best = _settings.DamagedLimbFocusFraction;
            bool found = false;
            DamageSettings damage = context.Settings.Damage;
            foreach (BodyPart limb in Limbs)
            {
                if (context.Opponent.IsSevered(limb)) continue;
                float durability = limb.IsArm() ? damage.ArmDurability : damage.LegDurability;
                if (durability <= 0f) continue;
                float share = context.Opponent.GetLimbDamage(limb) / durability;
                Vec2 where = board.LimbPoint(limb);
                if (share < best || !Reaches(context, weapon, where, move)) continue;
                best = share;
                point = where;
                found = true;
            }

            return found;
        }

        // --- Attack ------------------------------------------------------------------------------

        private WeaponPath DrawAttack(BotContext context, WeaponStats weapon, BodyMove move, Vec2 target)
        {
            PathSettings paths = context.Settings.Paths;
            ReachLimit reach = ReachLimit.For(paths, weapon);
            float radius = Math.Max(reach.RadiusUnits - ReachSafetyUnits, 0f);
            Vec2 tip = context.WeaponTips.TipLocal(weapon.WeaponId);

            // The path travels with the body (§6), so it is drawn where the target will be after the body move.
            Vec2 aim = target - Shift(context, move);
            var points = new List<Vec2> { tip };
            if (weapon.Kind == WeaponKind.Shield)
            {
                points.Add(ShieldRaisedLocal);
            }
            else if (!weapon.Rigidity.Enabled && Chance(context, _settings.SwingChance))
            {
                points.Add(aim + SwingStartFromTarget);
                points.Add(aim);
                points.Add(aim + SwingFollowThroughFromTarget);
            }
            else
            {
                Vec2 direction = Direction(aim - reach.OriginUnits);
                if (!weapon.Rigidity.Enabled) points.Add(aim - direction * ThrustApproachUnits);
                points.Add(aim + direction * ThrustThroughUnits);
            }

            for (int i = 1; i < points.Count; i++)
            {
                points[i] = Inside(points[i], reach.OriginUnits, radius);
            }

            FitInk(points, weapon.WithInkLengthMultiplier(context.Constraints.InkLengthMultiplier).InkLengthUnits * InkSafetyShare);
            if (Vec2.Distance(points[points.Count - 1], tip) < MinimumStrokeUnits)
                points.Add(Inside(tip + Direction(reach.OriginUnits + Vec2.UnitX * radius - tip) * MinimumStrokeUnits * 2f, reach.OriginUnits, radius));
            return new WeaponPath(points);
        }

        /// <summary>Scales every point toward the first (the tip) until the stroke fits the ink; that keeps it inside the reach circle too.</summary>
        private static void FitInk(List<Vec2> points, float budgetUnits)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Vec2.Distance(points[i - 1], points[i]);
            }

            if (length <= budgetUnits || length <= 0f) return;
            float fit = Math.Max(budgetUnits, 0f) / length;
            Vec2 tip = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                points[i] = tip + (points[i] - tip) * fit;
            }
        }

        // --- Reading the board -------------------------------------------------------------------

        private static bool Reaches(BotContext context, WeaponStats weapon, Vec2 target, BodyMove move)
        {
            ReachLimit reach = ReachLimit.For(context.Settings.Paths, weapon);
            return Vec2.Distance(target - Shift(context, move), reach.OriginUnits) <= reach.RadiusUnits - ReachMarginUnits;
        }

        /// <summary>Where the body move carries the torso frame by the end of the turn (a jump lands where it started).</summary>
        private static Vec2 Shift(BotContext context, BodyMove move)
        {
            if (move == BodyMove.None || move == BodyMove.Jump) return Vec2.Zero;
            BodyMoveStats stats = context.Settings.FindBodyMove(move);
            return stats != null && !stats.IsLeanInPlace ? stats.DisplacementUnits : Vec2.Zero;
        }

        private bool IsLowOnHp(BotContext context) => context.Self.Hp <= context.Settings.Damage.MaxHp * _settings.LowHpFraction;

        private static bool Chance(BotContext context, float chance) => context.Random.NextFloat() < chance;

        private static WeaponStats Stats(BotContext context, WeaponId weapon) =>
            context.Settings.FindWeapon(weapon) ?? throw new InvalidOperationException($"The rules settings have no stats for weapon '{weapon}'.");

        private static Vec2 Inside(Vec2 point, Vec2 origin, float radius)
        {
            Vec2 offset = point - origin;
            return offset.Length > radius ? origin + offset.Normalized * radius : point;
        }

        private static Vec2 Direction(Vec2 vector) => vector.Length > 0f ? vector.Normalized : Vec2.UnitX;

        /// <summary>The opponent's body as this side sees it, in this side's turn-start torso frame (+X toward the opponent).</summary>
        private sealed class Board
        {
            private readonly FighterPose _opponent;
            private readonly BodyPose _root;
            private readonly Side _side;
            private readonly float _armLengthUnits;

            public Board(BotContext context)
            {
                _side = context.Side;
                PoseSnapshot pose = context.Pose;
                FighterPose self = pose.Get(_side);
                _opponent = pose.Get(_side.Opponent());
                _root = TurnStartRoot.Of(self);
                _armLengthUnits = context.Settings.Paths.ArmLengthUnits;

                Vec2 pelvis = Local(_opponent.Get(BodyPart.Torso).PositionUnits);
                Vec2 neck = Local(_opponent.Get(BodyPart.Head).PositionUnits);
                OpponentTorso = (pelvis + neck) * 0.5f;
                OpponentLowTorso = pelvis + (neck - pelvis) * LowTargetShareOfTorso;
                OpponentHead = neck + (neck - pelvis) * HeadAboveNeckShareOfTorso;

                OpponentWeapon = context.Settings.FindWeapon(context.OpponentPublicState.Weapon) ?? context.Settings.FindWeapon(context.Opponent.CurrentWeapon);
                Threatened = OpponentWeapon != null && OpponentWeapon.Kind != WeaponKind.Shield && OpponentReaches(context, self);
            }

            public Vec2 OpponentTorso { get; }
            public Vec2 OpponentLowTorso { get; }
            public Vec2 OpponentHead { get; }

            /// <summary>The opponent's weapon as shown (null when the settings do not know it).</summary>
            public WeaponStats OpponentWeapon { get; }

            /// <summary>The opponent's shown weapon can reach this dummy's torso or head this turn, with a lunge if it may lunge.</summary>
            public bool Threatened { get; }

            /// <summary>Where to strike a limb: its elbow or knee (ten bodies), else half an arm's length down from its joint.</summary>
            public Vec2 LimbPoint(BodyPart limb)
            {
                if (_opponent.HasLowerSegments) return Local(_opponent.GetLower(limb).PositionUnits);
                BodyPose upper = _opponent.Get(limb);
                return Local(upper.PositionUnits + Vec2.FromAngleDegrees(upper.RotationDegrees - LimbAxisOffsetDegrees) * (_armLengthUnits * LimbStrikeShareOfArm));
            }

            private Vec2 Local(Vec2 arena) => TorsoFrame.ToLocal(arena, _root, _side);

            private bool OpponentReaches(BotContext context, FighterPose self)
            {
                Side opponent = _side.Opponent();
                BodyPose opponentRoot = TurnStartRoot.Of(_opponent);
                ReachLimit reach = ReachLimit.For(context.Settings.Paths, OpponentWeapon);
                BodyMoveStats lunge = context.Settings.FindBodyMove(BodyMove.Lunge);
                float lungeUnits = lunge != null ? Math.Max(lunge.DisplacementUnits.X, 0f) : 0f;
                Vec2 shoulder = TorsoFrame.ToArena(reach.OriginUnits, opponentRoot, opponent);
                Vec2 pelvis = self.Get(BodyPart.Torso).PositionUnits;
                Vec2 neck = self.Get(BodyPart.Head).PositionUnits;
                float distance = Math.Min(Vec2.Distance(shoulder, (pelvis + neck) * 0.5f), Vec2.Distance(shoulder, neck));
                return distance <= reach.RadiusUnits + lungeUnits;
            }
        }
    }
}
