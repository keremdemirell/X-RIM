using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Signature;
using XRim.Rules.Status;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Simulation.Scenarios;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// Boards and the match data around them, all exact: the rules state (in the plain shape a saved scenario uses,
    /// <see cref="ScenarioCodec"/>, so a field added there is added once), the frozen pose with every float as it is (the next turn is
    /// planned and simulated from it), the match setup, a side's planning limits and public state, an outcome and an execution report.
    /// </summary>
    internal static class BoardCodec
    {
        private const byte HasLowerSegmentsFlag = 1;
        private const byte HasHeldItemFlag = 2;
        private const byte HasDroppedItemFlag = 4;
        private const byte TorsoSplitFlag = 8;
        private const byte KnownFighterFlags = HasLowerSegmentsFlag | HasHeldItemFlag | HasDroppedItemFlag | TorsoSplitFlag;

        // --- Board -------------------------------------------------------------------------

        public static void WriteBoard(PacketWriter writer, BoardSnapshot board)
        {
            WriteState(writer, board.State);
            WritePose(writer, board.Pose);
        }

        public static BoardSnapshot ReadBoard(PacketReader reader) => new BoardSnapshot(ReadState(reader), ReadPose(reader));

        public static void WriteState(PacketWriter writer, MatchState state)
        {
            MatchStateData data = ScenarioCodec.StateToData(state);
            writer.WriteInt(data.TurnIndex);
            writer.WriteBool(data.IsSuddenDeath);
            writer.WriteInt(data.SuddenDeathRepeats);
            WriteFighter(writer, data.Left);
            WriteFighter(writer, data.Right);
            WriteWall(writer, data.LeftWall);
            WriteWall(writer, data.RightWall);
        }

        public static MatchState ReadState(PacketReader reader)
        {
            var data = new MatchStateData
            {
                TurnIndex = reader.ReadInt("the turn index"),
                IsSuddenDeath = reader.ReadBool("sudden death"),
                SuddenDeathRepeats = reader.ReadInt("the sudden-death repeats"),
                Left = ReadFighter(reader),
                Right = ReadFighter(reader),
                LeftWall = ReadWall(reader),
                RightWall = ReadWall(reader),
            };
            return ScenarioCodec.StateFromData(data);
        }

        private static void WriteFighter(PacketWriter writer, FighterStateData fighter)
        {
            writer.WriteInt((int)fighter.Handedness);
            writer.WriteFloat(fighter.Hp);
            writer.WriteFloat(fighter.HpBeforeSuddenDeath);
            writer.WriteText(fighter.CurrentWeapon, ProtocolLimits.MaxTextBytes, "the current weapon");
            writer.WriteFloat(fighter.HpDamageTaken);
            writer.WriteInt(fighter.LifestealHitCount);
            writer.WriteFloat(fighter.LifestealDamageDealt);
            writer.WriteBool(fighter.HitLandedThisTurn);
            writer.WriteFloat(fighter.SignatureCharge);
            writer.WriteInt(fighter.SignatureUses);
            writer.WriteInt(fighter.ConsecutiveIdleTurns);

            writer.WriteCount(fighter.LimbDamage.Length, ProtocolLimits.MaxShortListItems, "limb damage");
            foreach (float damage in fighter.LimbDamage)
            {
                writer.WriteFloat(damage);
            }

            writer.WriteCount(fighter.Severed.Length, ProtocolLimits.MaxShortListItems, "severed limbs");
            foreach (bool severed in fighter.Severed)
            {
                writer.WriteBool(severed);
            }

            writer.WriteCount(fighter.Statuses.Count, ProtocolLimits.MaxShortListItems, "status effects");
            foreach (StatusEffectData status in fighter.Statuses)
            {
                writer.WriteInt((int)status.Kind);
                writer.WriteInt((int)status.Effect);
                writer.WriteInt(status.RemainingTurns);
                writer.WriteFloat(status.Fraction);
            }

            writer.WriteBool(fighter.HasHeldLimb);
            writer.WriteInt((int)fighter.HeldLimbOwner);
            writer.WriteInt((int)fighter.HeldLimbPart);
            writer.WriteCount(fighter.LostWeapons.Count, ProtocolLimits.MaxShortListItems, "lost weapons");
            foreach (string weapon in fighter.LostWeapons)
            {
                writer.WriteText(weapon, ProtocolLimits.MaxTextBytes, "a lost weapon");
            }
        }

        private static FighterStateData ReadFighter(PacketReader reader)
        {
            var fighter = new FighterStateData
            {
                Handedness = reader.ReadEnum<Handedness>("a handedness"),
                Hp = reader.ReadFloat(),
                HpBeforeSuddenDeath = reader.ReadFloat(),
                CurrentWeapon = reader.ReadText(ProtocolLimits.MaxTextBytes, "the current weapon"),
                HpDamageTaken = reader.ReadFloat(),
                LifestealHitCount = reader.ReadInt("the lifesteal hit count"),
                LifestealDamageDealt = reader.ReadFloat(),
                HitLandedThisTurn = reader.ReadBool("hit landed this turn"),
                SignatureCharge = reader.ReadFloat(),
                SignatureUses = reader.ReadInt("the signature uses"),
                ConsecutiveIdleTurns = reader.ReadInt("the idle turns"),
            };

            fighter.LimbDamage = new float[reader.ReadCount(ProtocolLimits.MaxShortListItems, "limb damage", 4)];
            for (int i = 0; i < fighter.LimbDamage.Length; i++)
            {
                fighter.LimbDamage[i] = reader.ReadFloat();
            }

            fighter.Severed = new bool[reader.ReadCount(ProtocolLimits.MaxShortListItems, "severed limbs")];
            for (int i = 0; i < fighter.Severed.Length; i++)
            {
                fighter.Severed[i] = reader.ReadBool("a severed limb");
            }

            int statuses = reader.ReadCount(ProtocolLimits.MaxShortListItems, "status effects", 7);
            for (int i = 0; i < statuses; i++)
            {
                fighter.Statuses.Add(new StatusEffectData
                {
                    Kind = reader.ReadEnum<StatusKind>("a status kind"),
                    Effect = reader.ReadEnum<StunEffect>("a status effect"),
                    RemainingTurns = reader.ReadInt("a status's turns"),
                    Fraction = reader.ReadFloat(),
                });
            }

            fighter.HasHeldLimb = reader.ReadBool("a held limb");
            fighter.HeldLimbOwner = reader.ReadEnum<Side>("the held limb's owner");
            fighter.HeldLimbPart = reader.ReadEnum<BodyPart>("the held limb");
            int lost = reader.ReadCount(ProtocolLimits.MaxShortListItems, "lost weapons");
            for (int i = 0; i < lost; i++)
            {
                fighter.LostWeapons.Add(reader.ReadText(ProtocolLimits.MaxTextBytes, "a lost weapon"));
            }

            return fighter;
        }

        private static void WriteWall(PacketWriter writer, ElectricWallStateData wall)
        {
            writer.WriteInt((int)wall.Phase);
            writer.WriteBool(wall.IsUp);
            writer.WriteFloat(wall.PositionUnits);
            writer.WriteInt(wall.ConsecutiveRetreats);
        }

        private static ElectricWallStateData ReadWall(PacketReader reader) => new ElectricWallStateData
        {
            Phase = reader.ReadEnum<ElectricWallPhase>("a wall phase"),
            IsUp = reader.ReadBool("a wall standing"),
            PositionUnits = reader.ReadFloat(),
            ConsecutiveRetreats = reader.ReadInt("a wall's retreats"),
        };

        // --- Exact pose ------------------------------------------------------------------

        public static void WritePose(PacketWriter writer, PoseSnapshot pose)
        {
            WriteFighterPose(writer, pose.Left);
            WriteFighterPose(writer, pose.Right);
            writer.WriteCount(pose.SeveredLimbs.Count, ProtocolLimits.MaxSeveredLimbs, "severed limbs on the floor");
            foreach (SeveredLimbPose limb in pose.SeveredLimbs)
            {
                writer.WriteInt((int)limb.Owner);
                writer.WriteInt((int)limb.Part);
                WriteBodyPose(writer, limb.Pose);
                writer.WriteBool(limb.HasLower);
                WriteBodyPose(writer, limb.Lower);
            }

            WriteWallPose(writer, pose.LeftWall);
            WriteWallPose(writer, pose.RightWall);
        }

        public static PoseSnapshot ReadPose(PacketReader reader)
        {
            var pose = new PoseSnapshot { Left = ReadFighterPose(reader), Right = ReadFighterPose(reader) };
            int limbs = reader.ReadCount(ProtocolLimits.MaxSeveredLimbs, "severed limbs on the floor", 27);
            for (int i = 0; i < limbs; i++)
            {
                pose.SeveredLimbs.Add(new SeveredLimbPose
                {
                    Owner = reader.ReadEnum<Side>("a limb's owner"),
                    Part = reader.ReadEnum<BodyPart>("a limb"),
                    Pose = ReadBodyPose(reader),
                    HasLower = reader.ReadBool("a limb's lower segment"),
                    Lower = ReadBodyPose(reader),
                });
            }

            pose.LeftWall = ReadWallPose(reader);
            pose.RightWall = ReadWallPose(reader);
            return pose;
        }

        private static void WriteFighterPose(PacketWriter writer, FighterPose fighter)
        {
            writer.WriteByte((byte)((fighter.HasLowerSegments ? HasLowerSegmentsFlag : 0) | (fighter.HasHeldItem ? HasHeldItemFlag : 0) |
                                    (fighter.HasDroppedItem ? HasDroppedItemFlag : 0) | (fighter.IsTorsoSplit ? TorsoSplitFlag : 0)));
            writer.WriteInt(fighter.SeveredParts);
            for (int i = 0; i < BodyParts.Count; i++)
            {
                WriteBodyPose(writer, fighter.Parts[i]);
            }

            for (int i = 0; i < BodyParts.Count; i++)
            {
                WriteBodyPose(writer, fighter.LowerSegments[i]);
            }

            WriteBodyPose(writer, fighter.HeldItem);
            WriteBodyPose(writer, fighter.DroppedItem);
            WriteBodyPose(writer, fighter.TorsoTop);
        }

        private static FighterPose ReadFighterPose(PacketReader reader)
        {
            byte flags = reader.ReadByte();
            if ((flags & ~KnownFighterFlags) != 0) throw PacketReader.Malformed($"A dummy's pose has unknown flags ({flags}).");
            var fighter = new FighterPose
            {
                HasLowerSegments = (flags & HasLowerSegmentsFlag) != 0,
                HasHeldItem = (flags & HasHeldItemFlag) != 0,
                HasDroppedItem = (flags & HasDroppedItemFlag) != 0,
                IsTorsoSplit = (flags & TorsoSplitFlag) != 0,
                SeveredParts = reader.ReadInt("the severed parts"),
            };
            for (int i = 0; i < BodyParts.Count; i++)
            {
                fighter.Parts[i] = ReadBodyPose(reader);
            }

            for (int i = 0; i < BodyParts.Count; i++)
            {
                fighter.LowerSegments[i] = ReadBodyPose(reader);
            }

            fighter.HeldItem = ReadBodyPose(reader);
            fighter.DroppedItem = ReadBodyPose(reader);
            fighter.TorsoTop = ReadBodyPose(reader);
            return fighter;
        }

        private static void WriteWallPose(PacketWriter writer, ElectricWallPose wall)
        {
            writer.WriteBool(wall.IsUp);
            writer.WriteInt((int)wall.Phase);
            writer.WriteFloat(wall.XUnits);
        }

        private static ElectricWallPose ReadWallPose(PacketReader reader) => new ElectricWallPose
        {
            IsUp = reader.ReadBool("a wall standing"),
            Phase = reader.ReadEnum<ElectricWallPhase>("a wall phase"),
            XUnits = reader.ReadFloat(),
        };

        public static void WriteBodyPose(PacketWriter writer, BodyPose pose)
        {
            WriteVec2(writer, pose.PositionUnits);
            writer.WriteFloat(pose.RotationDegrees);
        }

        public static BodyPose ReadBodyPose(PacketReader reader) => new BodyPose(ReadVec2(reader), reader.ReadFloat());

        public static void WriteVec2(PacketWriter writer, Vec2 value)
        {
            writer.WriteFloat(value.X);
            writer.WriteFloat(value.Y);
        }

        public static Vec2 ReadVec2(PacketReader reader) => new Vec2(reader.ReadFloat(), reader.ReadFloat());

        // --- Match setup, limits, public state, outcome, report ----------------------------

        public static void WriteSetup(PacketWriter writer, MatchSetup setup)
        {
            WriteFighterSetup(writer, setup.Fighters.Left);
            WriteFighterSetup(writer, setup.Fighters.Right);
            writer.WriteVarUInt(setup.RandomSeed);
        }

        public static MatchSetup ReadSetup(PacketReader reader)
        {
            FighterSetup left = ReadFighterSetup(reader);
            FighterSetup right = ReadFighterSetup(reader);
            return new MatchSetup(new PerSide<FighterSetup>(left, right), reader.ReadUInt("the match seed"));
        }

        private static void WriteFighterSetup(PacketWriter writer, FighterSetup fighter)
        {
            writer.WriteInt((int)fighter.Handedness);
            WriteWeapons(writer, fighter.Loadout, "a loadout");
            writer.WriteBool(fighter.Signatures != null);
            if (fighter.Signatures != null) WriteSignatureIds(writer, fighter.Signatures, "signature moves brought");
        }

        private static FighterSetup ReadFighterSetup(PacketReader reader)
        {
            var handedness = reader.ReadEnum<Handedness>("a handedness");
            List<WeaponId> loadout = ReadWeapons(reader, "a loadout");
            List<SignatureMoveId> signatures = reader.ReadBool("signature moves brought") ? ReadSignatureIds(reader, "signature moves brought") : null;
            return new FighterSetup(handedness, loadout, signatures);
        }

        public static void WriteConstraints(PacketWriter writer, PlanningConstraints constraints)
        {
            writer.WriteFloat(constraints.PlanningDurationSeconds);
            writer.WriteFloat(constraints.InkLengthMultiplier);
            writer.WriteBool(constraints.CanSwitchWeapon);
            writer.WriteBool(constraints.CanUseSignature);
            writer.WriteBool(constraints.CanUseLoadoutWeapons);
            WriteWeapons(writer, constraints.ExtraWeapons, "extra weapons");
            writer.WriteCount(constraints.Statuses.Count, ProtocolLimits.MaxShortListItems, "statuses");
            foreach (StatusKind status in constraints.Statuses)
            {
                writer.WriteInt((int)status);
            }

            IReadOnlyList<BodyMove> forbidden = constraints.ForbiddenBodyMoves;
            writer.WriteCount(forbidden.Count, ProtocolLimits.MaxShortListItems, "forbidden body moves");
            foreach (BodyMove move in forbidden)
            {
                writer.WriteInt((int)move);
            }

            writer.WriteBool(constraints.AreAllBodyMovesForbidden);
            WriteWeapons(writer, constraints.ForbiddenWeapons, "forbidden weapons");
        }

        public static PlanningConstraints ReadConstraints(PacketReader reader)
        {
            var constraints = new PlanningConstraints
            {
                PlanningDurationSeconds = reader.ReadFloat(),
                InkLengthMultiplier = reader.ReadFloat(),
                CanSwitchWeapon = reader.ReadBool("weapon switching"),
                CanUseSignature = reader.ReadBool("signature moves"),
                CanUseLoadoutWeapons = reader.ReadBool("loadout weapons"),
            };
            foreach (WeaponId weapon in ReadWeapons(reader, "extra weapons"))
            {
                constraints.AddExtraWeapon(weapon);
            }

            int statuses = reader.ReadCount(ProtocolLimits.MaxShortListItems, "statuses");
            for (int i = 0; i < statuses; i++)
            {
                constraints.AddStatus(reader.ReadEnum<StatusKind>("a status"));
            }

            int forbidden = reader.ReadCount(ProtocolLimits.MaxShortListItems, "forbidden body moves");
            for (int i = 0; i < forbidden; i++)
            {
                constraints.ForbidBodyMove(reader.ReadEnum<BodyMove>("a forbidden body move"));
            }

            if (reader.ReadBool("all body moves forbidden")) constraints.ForbidAllBodyMoves();
            foreach (WeaponId weapon in ReadWeapons(reader, "forbidden weapons"))
            {
                constraints.ForbidWeapon(weapon);
            }

            return constraints;
        }

        public static void WritePublicState(PacketWriter writer, PublicPlanningState state)
        {
            WriteWeapon(writer, state.Weapon);
            writer.WriteBool(state.IsReady);
            writer.WriteBool(state.SignatureChargedVisible);
        }

        public static PublicPlanningState ReadPublicState(PacketReader reader) =>
            new PublicPlanningState(ReadWeapon(reader), reader.ReadBool("Ready"), reader.ReadBool("a charge shown"));

        public static void WriteCharge(PacketWriter writer, SignatureChargeState charge)
        {
            writer.WriteBool(charge.IsAvailable);
            writer.WriteFloat(charge.ChargeFraction);
        }

        public static SignatureChargeState ReadCharge(PacketReader reader) =>
            new SignatureChargeState(reader.ReadBool("a charge available"), reader.ReadFloat());

        public static void WriteOutcome(PacketWriter writer, MatchOutcome outcome)
        {
            writer.WriteInt((int)outcome.Winner);
            writer.WriteInt((int)outcome.Reason);
            writer.WriteInt(outcome.TurnCount);
        }

        public static MatchOutcome ReadOutcome(PacketReader reader) =>
            new MatchOutcome(reader.ReadEnum<Side>("the winner"), reader.ReadEnum<MatchEndReason>("the end reason"), reader.ReadInt("the turn count"));

        public static void WriteReport(PacketWriter writer, ExecutionReport report)
        {
            WriteState(writer, report.ResolvedState);
            WriteOptionalTime(writer, report.FirstValidHitTime.Left);
            WriteOptionalTime(writer, report.FirstValidHitTime.Right);
            writer.WriteBool(report.FirstHitsOnSameStep);
            WriteOptionalTime(writer, report.DeathTime.Left);
            WriteOptionalTime(writer, report.DeathTime.Right);
        }

        public static ExecutionReport ReadReport(PacketReader reader)
        {
            MatchState state = ReadState(reader);
            var firstHits = new PerSide<SimTime?>(ReadOptionalTime(reader), ReadOptionalTime(reader));
            bool sameStep = reader.ReadBool("first hits on the same step");
            var deaths = new PerSide<SimTime?>(ReadOptionalTime(reader), ReadOptionalTime(reader));
            return new ExecutionReport(state, firstHits, sameStep, deaths);
        }

        private static void WriteOptionalTime(PacketWriter writer, SimTime? time)
        {
            writer.WriteBool(time.HasValue);
            if (time.HasValue) writer.WriteVarInt(time.Value.Microseconds);
        }

        private static SimTime? ReadOptionalTime(PacketReader reader) =>
            reader.ReadBool("a time") ? new SimTime(reader.ReadVarInt()) : (SimTime?)null;

        // --- Ids ---------------------------------------------------------------------------

        public static void WriteWeapon(PacketWriter writer, WeaponId weapon) =>
            writer.WriteText(weapon.Value, ProtocolLimits.MaxTextBytes, "a weapon id");

        public static WeaponId ReadWeapon(PacketReader reader) => new WeaponId(reader.ReadText(ProtocolLimits.MaxTextBytes, "a weapon id"));

        public static void WriteSignature(PacketWriter writer, SignatureMoveId move) =>
            writer.WriteText(move.Value, ProtocolLimits.MaxTextBytes, "a signature move id");

        public static SignatureMoveId ReadSignature(PacketReader reader) =>
            new SignatureMoveId(reader.ReadText(ProtocolLimits.MaxTextBytes, "a signature move id"));

        public static void WriteWeapons(PacketWriter writer, IReadOnlyList<WeaponId> weapons, string what)
        {
            writer.WriteCount(weapons.Count, ProtocolLimits.MaxShortListItems, what);
            foreach (WeaponId weapon in weapons)
            {
                WriteWeapon(writer, weapon);
            }
        }

        public static List<WeaponId> ReadWeapons(PacketReader reader, string what)
        {
            int count = reader.ReadCount(ProtocolLimits.MaxShortListItems, what);
            var weapons = new List<WeaponId>(count);
            for (int i = 0; i < count; i++)
            {
                weapons.Add(ReadWeapon(reader));
            }

            return weapons;
        }

        public static void WriteSignatureIds(PacketWriter writer, IReadOnlyList<SignatureMoveId> moves, string what)
        {
            writer.WriteCount(moves.Count, ProtocolLimits.MaxShortListItems, what);
            foreach (SignatureMoveId move in moves)
            {
                WriteSignature(writer, move);
            }
        }

        public static List<SignatureMoveId> ReadSignatureIds(PacketReader reader, string what)
        {
            int count = reader.ReadCount(ProtocolLimits.MaxShortListItems, what);
            var moves = new List<SignatureMoveId>(count);
            for (int i = 0; i < count; i++)
            {
                moves.Add(ReadSignature(reader));
            }

            return moves;
        }
    }
}
