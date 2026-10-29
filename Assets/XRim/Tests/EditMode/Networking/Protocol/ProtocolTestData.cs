using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Signature;
using XRim.Rules.Status;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Tests.EditMode.Simulation.Scenarios;

namespace XRim.Tests.EditMode.Networking.Protocol
{
    /// <summary>Messages of every kind with every part of their contents set, a synthetic full-length turn, and helpers for hand-made bytes.</summary>
    internal static class ProtocolTestData
    {
        public const int FullLengthSteps = 360;

        /// <summary>A pessimistic wobble for a ragdoll held by servos: random noise cannot be packed, so it is what makes a turn large.</summary>
        public const float DefaultJitterUnits = 0.15f;

        private const double StepRateHz = 240.0;
        private const float JitterDegreesPerUnit = 4f / 3f;

        /// <summary>One message of each kind, every field set to something other than its default.</summary>
        public static IEnumerable<NetMessage> OneOfEachKind()
        {
            RulesSettings rules = GddStartingValues.CreateRulesSettings();
            BoardSnapshot board = ScenarioTestData.RichBoard(rules, new SimulationSettings());

            yield return new ClientHelloMessage(Handedness.Left,
                new[] { SignatureMoveIds.FiveFingerExplodingTouch, SignatureMoveIds.WhiplashCalibrationSpin }, "token-7f3a");
            yield return new LoadoutPickMessage(new[] { WeaponIds.Spear, WeaponIds.Mace, WeaponIds.Shield });
            foreach (PlanningCommand command in OneOfEachCommand())
            {
                yield return new PlanningCommandMessage(42, 17, command);
            }

            yield return new PlaybackFinishedMessage(17);
            yield return new PingMessage(3, 1234.5625);
            yield return new MatchFoundMessage(Side.Right, "token-7f3a", "{\"rules\":\"ünïcødé ✓\",\"n\":12.5}");
            yield return new MatchStartedMessage(RichSetup(), board);
            yield return new PlanningStartedMessage(17, 11.75, 5.875, true, RichConstraints(), board,
                new SignatureOffer(new[] { SignatureMoveIds.HeadImpactSimulation404 }, new SignatureChargeState(true, 0.625f), true),
                new PerSide<PublicPlanningState>(new PublicPlanningState(WeaponIds.SeveredLimb, true, false),
                    new PublicPlanningState(WeaponIds.Mace, false, true)));
            yield return new PublicStateMessage(Side.Right, new PublicPlanningState(WeaponIds.Shield, true, true));
            yield return new CommandAnswerMessage(42, CommandRejection.WeaponSwitchLockedOut);
            yield return new PlanStateMessage(17, WeaponIds.Rapier, BodyMove.Crouch, Stroke(), SignatureMoveIds.HeadImpactSimulation404, true);
            yield return new PlanningLockedMessage(17);
            yield return new TurnResolvedMessage(ScenarioTestData.TurnWithEveryEvent());
            yield return new MatchEndedMessage(new MatchOutcome(Side.Right, MatchEndReason.SuddenDeathCoinToss, 31));
            yield return new ConnectionNoticeMessage(Side.Left, ConnectionStatus.StoppedWaiting, 12.5f);
            yield return new PongMessage(3, 1234.5625, 98765.125);
            yield return new GoodbyeMessage(GoodbyeReason.VersionMismatch, "client 2, server 1");
        }

        public static IEnumerable<PlanningCommand> OneOfEachCommand()
        {
            yield return new SelectWeaponCommand(WeaponIds.Spear);
            yield return new SetBodyMoveCommand(BodyMove.StepBack);
            yield return new SetPathCommand(new WeaponPath(Stroke()));
            yield return new ClearPathCommand();
            yield return new SetReadyCommand(true);
            yield return new UseSignatureCommand(SignatureMoveIds.WhiplashCalibrationSpin);
        }

        /// <summary>A stroke as a finger draws it: a point every unit or so, values with long fractions.</summary>
        public static Vec2[] Stroke(int points = 240)
        {
            var stroke = new Vec2[points];
            for (int i = 0; i < points; i++)
            {
                stroke[i] = new Vec2(80.123f + i * 1.0371f, 95.5f + 40f * (float)Math.Sin(i * 0.031));
            }

            return stroke;
        }

        public static MatchSetup RichSetup() => new MatchSetup(new PerSide<FighterSetup>(
                new FighterSetup(Handedness.Left, new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield },
                    new[] { SignatureMoveIds.FiveFingerExplodingTouch }),
                new FighterSetup(Handedness.Right, new[] { WeaponIds.Sword, WeaponIds.Spear, WeaponIds.Shield })),
            4000000007u);

        /// <summary>Left stunned (no moves at all), right with forbidden moves, a lost weapon, an extra option, less ink and no switching.</summary>
        public static PerSide<PlanningConstraints> RichConstraints()
        {
            var left = new PlanningConstraints { PlanningDurationSeconds = 6f };
            left.ForbidAllBodyMoves();
            left.AddStatus(StatusKind.Stunned);
            left.AddStatus(StatusKind.Staggered);

            var right = new PlanningConstraints
            {
                PlanningDurationSeconds = 12f, InkLengthMultiplier = 0.5f, CanSwitchWeapon = false, CanUseSignature = false,
                CanUseLoadoutWeapons = false,
            };
            right.ForbidBodyMove(BodyMove.Jump);
            right.ForbidBodyMove(BodyMove.Lunge);
            right.ForbidWeapon(WeaponIds.Spear);
            right.ForbidWeapon(WeaponIds.Mace);
            right.AddExtraWeapon(WeaponIds.SeveredLimb);
            right.AddExtraWeapon(WeaponIds.BodyBlow);
            return new PerSide<PlanningConstraints>(left, right);
        }

        /// <summary>The turn as a client receives it: every frame's pose rounded, everything else unchanged.</summary>
        public static TurnResult WithFramesRounded(TurnResult result)
        {
            var frames = new List<TimelineFrame>();
            foreach (TimelineFrame frame in result.Timeline.Frames)
            {
                frames.Add(new TimelineFrame(frame.Step, frame.Time, PoseRounding.Round(frame.Pose)));
            }

            return new TurnResult(result.TurnIndex, new TurnTimeline(frames, result.Timeline.Events), result.FinalBoard, result.Report,
                result.EndReason);
        }

        /// <summary>
        /// A synthetic turn as long as the execution hard cap allows (1.5 s, 361 frames at 240 Hz), with ten-body dummies: the left one lunges
        /// and swings its weapon through a wide arc, the right one is struck at 0.5 s, knocked back and wobbles, its left arm is severed at
        /// 0.6 s and flies off spinning until it lands, the left wall stands as a warning, and every body jitters a little the way a ragdoll
        /// held by servos does. It stands in for a real recording until the server measures real ones (Batch 5).
        /// </summary>
        /// <param name="jitterUnits">How far each body wobbles at random each frame (rotations a little more); 0 for perfectly smooth motion.</param>
        public static TurnResult FullLengthTurn(float jitterUnits = DefaultJitterUnits)
        {
            RulesSettings rules = GddStartingValues.CreateRulesSettings();
            var simulation = new SimulationSettings();
            MatchState state = MatchState.CreateInitial(TestData.CreateMatchSetup(), rules);
            PoseSnapshot start = StartingBoard.Create(state, rules, simulation, new AimFromShoulderModel());
            start.LeftWall = new ElectricWallPose(ElectricWallPhase.Warning, -812.5f);
            var jitter = new Random(1234);

            var frames = new List<TimelineFrame>();
            for (int step = 0; step <= FullLengthSteps; step++)
            {
                double t = step / StepRateHz;
                PoseSnapshot pose = start.Clone();
                MoveAttacker(pose.Left, t);
                MoveVictim(pose.Right, t);
                if (t >= 0.6) pose.SeveredLimbs.Add(FlyingArm(start.Right.Get(BodyPart.LeftArm), t - 0.6));
                if (t >= 0.6) pose.Right.MarkSevered(BodyPart.LeftArm);
                Jitter(pose.Left, jitter, jitterUnits);
                Jitter(pose.Right, jitter, jitterUnits);
                frames.Add(new TimelineFrame(step, SimTime.FromSeconds(t), pose));
            }

            var events = new List<MatchEvent>
            {
                new BodyMoveStartedEvent(SimTime.Zero, Side.Left, BodyMove.Lunge),
                new AttackStartedEvent(SimTime.Zero, Side.Left, WeaponIds.Rapier, WeaponKind.Weapon),
                new AttackStartedEvent(SimTime.Zero, Side.Right, WeaponIds.Mace, WeaponKind.Weapon),
            };
            for (int i = 0; i < 12; i++)
            {
                var a = new BodyTag(Side.Left, BodyRole.HeldItem, BodyPart.Torso);
                var b = new BodyTag(Side.Right, BodyRole.BodyPart, (BodyPart)(i % BodyParts.Count));
                SimTime time = SimTime.FromSeconds(0.45 + i * 0.01);
                var facts = new ContactFacts(a, b, new Vec2(120.5f + i, 210.25f - i), new Vec2(0.6f, -0.8f), new Vec2(-850.5f, 120.25f));
                events.Add(new ContactEvent(new TurnContact(facts, 108 + i, time, Side.Left, 380.5f + i, 64.5f)));
            }

            SimTime hit = SimTime.FromSeconds(0.5);
            events.Add(new HitLandedEvent(hit, new HitFacts(Side.Left, BodyPart.LeftArm, WeaponIds.Rapier, hit, false),
                new DamageResult(6.4f, 6.4f, 6.4f, false, true)));
            events.Add(new LimbSeveredEvent(SimTime.FromSeconds(0.6), Side.Right, BodyPart.LeftArm));
            events.Add(new AttackInterruptedEvent(SimTime.FromSeconds(0.6), Side.Right));

            PoseSnapshot last = frames[frames.Count - 1].Pose.Clone();
            state.Fighters.Right.MarkSevered(BodyPart.LeftArm);
            var final = new BoardSnapshot(state, last);
            var report = new ExecutionReport(state.Clone(), new PerSide<SimTime?>(hit, null), false);
            return new TurnResult(3, new TurnTimeline(frames, events), final, report, TurnEndReason.HardCap);
        }

        /// <summary>How many plain 4-byte numbers the frames hold: what they would take sent as they are.</summary>
        public static int PlainFrameBytes(TurnResult result)
        {
            const int numbersPerPose = 3;
            const int posesPerFighter = 2 * BodyParts.Count + 3;
            int numbers = 0;
            foreach (TimelineFrame frame in result.Timeline.Frames)
            {
                numbers += 2 * posesPerFighter * numbersPerPose + frame.Pose.SeveredLimbs.Count * 2 * numbersPerPose + 2;
            }

            return numbers * 4;
        }

        // --- Hand-made bytes ------------------------------------------------------------------

        /// <summary>A message's header for this kind (unpacked) followed by the body bytes given.</summary>
        public static byte[] Message(MessageKind kind, params byte[] body)
        {
            var bytes = new byte[ProtocolCodec.HeaderBytes + body.Length];
            bytes[0] = (byte)ProtocolCodec.CurrentVersion;
            bytes[1] = (byte)(ProtocolCodec.CurrentVersion >> 8);
            bytes[2] = (byte)kind;
            Array.Copy(body, 0, bytes, ProtocolCodec.HeaderBytes, body.Length);
            return bytes;
        }

        /// <summary>An unsigned number as the protocol writes it (seven bits a byte).</summary>
        public static byte[] VarUInt(ulong value)
        {
            var bytes = new List<byte>();
            while (value >= 0x80)
            {
                bytes.Add((byte)(value | 0x80));
                value >>= 7;
            }

            bytes.Add((byte)value);
            return bytes.ToArray();
        }

        /// <summary>A signed number as the protocol writes it (zigzag, then seven bits a byte).</summary>
        public static byte[] VarInt(long value) => VarUInt((ulong)((value << 1) ^ (value >> 63)));

        public static byte[] Concat(params byte[][] parts)
        {
            var bytes = new List<byte>();
            foreach (byte[] part in parts)
            {
                bytes.AddRange(part);
            }

            return bytes.ToArray();
        }

        // --- The synthetic turn's motion ----------------------------------------------------------

        private static void MoveAttacker(FighterPose fighter, double t)
        {
            float lunge = 120f * (float)Ease(t / 0.35);
            float swing = (float)Ease(t / 0.6);
            Shift(fighter, lunge, -20f * (float)Ease(t / 0.35), 12f * (float)Ease(t / 0.35));
            BodyPose held = fighter.HeldItem;
            float angle = held.RotationDegrees + 110f * swing;
            fighter.HeldItem = new BodyPose(held.PositionUnits + new Vec2(lunge + 60f * (float)Math.Sin(swing * Math.PI), 140f * swing), angle);
            BodyPart arm = BodyPart.RightArm;
            fighter.Set(arm, Turned(fighter.Get(arm), 95f * swing));
            fighter.SetLower(arm, Turned(fighter.GetLower(arm), 120f * swing));
        }

        private static void MoveVictim(FighterPose fighter, double t)
        {
            if (t < 0.5) return;
            double since = t - 0.5;
            float knock = -90f * (float)(1.0 - Math.Exp(-since / 0.08));
            float wobble = 6f * (float)(Math.Exp(-since / 0.2) * Math.Sin(2.0 * Math.PI * 4.0 * since));
            Shift(fighter, knock, 0f, wobble);
            fighter.Set(BodyPart.Head, Turned(fighter.Get(BodyPart.Head), 2f * wobble));
        }

        private static SeveredLimbPose FlyingArm(BodyPose atSever, double since)
        {
            const float floorY = 20f;
            const float gravity = 3924f;
            float y = atSever.PositionUnits.Y + 320f * (float)since - 0.5f * gravity * (float)(since * since);
            bool landed = y <= floorY;
            double flight = landed ? Math.Min(since, (320.0 + Math.Sqrt(320.0 * 320.0 + 2.0 * gravity * (atSever.PositionUnits.Y - floorY))) / gravity) : since;
            float x = atSever.PositionUnits.X + 200f * (float)flight;
            var upper = new BodyPose(new Vec2(x, landed ? floorY : y), atSever.RotationDegrees + 600f * (float)flight);
            var lower = new BodyPose(upper.PositionUnits + new Vec2(0f, -90f), upper.RotationDegrees + 35f);
            return new SeveredLimbPose(Side.Right, BodyPart.LeftArm, upper, lower);
        }

        private static void Shift(FighterPose fighter, float dx, float dy, float leanDegrees)
        {
            var offset = new Vec2(dx, dy);
            for (int i = 0; i < BodyParts.Count; i++)
            {
                fighter.Parts[i] = new BodyPose(fighter.Parts[i].PositionUnits + offset, fighter.Parts[i].RotationDegrees + leanDegrees);
                fighter.LowerSegments[i] = new BodyPose(fighter.LowerSegments[i].PositionUnits + offset, fighter.LowerSegments[i].RotationDegrees);
            }
        }

        private static void Jitter(FighterPose fighter, Random random, float units)
        {
            if (units <= 0f) return;
            for (int i = 0; i < BodyParts.Count; i++)
            {
                fighter.Parts[i] = Jittered(fighter.Parts[i], random, units);
                fighter.LowerSegments[i] = Jittered(fighter.LowerSegments[i], random, units);
            }

            fighter.HeldItem = Jittered(fighter.HeldItem, random, units);
        }

        private static BodyPose Jittered(BodyPose pose, Random random, float units) => new BodyPose(
            pose.PositionUnits + new Vec2(Noise(random, units), Noise(random, units)), pose.RotationDegrees + Noise(random, units * JitterDegreesPerUnit));

        private static float Noise(Random random, float amplitude) => (float)(random.NextDouble() * 2.0 - 1.0) * amplitude;

        private static BodyPose Turned(BodyPose pose, float degrees) => new BodyPose(pose.PositionUnits, pose.RotationDegrees + degrees);

        private static double Ease(double fraction)
        {
            double x = Math.Max(0.0, Math.Min(1.0, fraction));
            return x * x * (3.0 - 2.0 * x);
        }
    }
}
