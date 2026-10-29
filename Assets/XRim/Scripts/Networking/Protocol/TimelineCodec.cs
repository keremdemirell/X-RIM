using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// A turn's recorded frames, every one (slow motion shows them one by one, so none is dropped). Each position and rotation is rounded
    /// (<see cref="PoseRounding"/>) and sent as its difference from where its own motion over the two frames before predicts it, which is
    /// zero or nearly zero for anything moving smoothly or lying still; the packed message then shrinks those to almost nothing. Flags,
    /// steps and times travel exactly. The reader rebuilds the same rounded poses.
    /// </summary>
    internal static class TimelineCodec
    {
        private const int ValuesPerPose = 3;

        /// <summary>A fighter's poses in the order they travel: six parts, six lower segments, the held item, a dropped item, the torso's top half.</summary>
        private const int PosesPerFighter = 2 * BodyPartCount + 3;

        private const int BodyPartCount = BodyParts.Count;
        private const int ChannelsPerFighter = PosesPerFighter * ValuesPerPose;
        private const int LimbChannelsStart = 2 * ChannelsPerFighter;

        /// <summary>A severed limb's two segments for each owner and part, so a limb keeps its history while it lies or flies.</summary>
        private const int LimbChannels = 2 * BodyPartCount * 2 * ValuesPerPose;

        private const int WallChannelsStart = LimbChannelsStart + LimbChannels;
        private const int ChannelCount = WallChannelsStart + 2;

        private const byte HasLowerSegmentsFlag = 1;
        private const byte HasHeldItemFlag = 2;
        private const byte HasDroppedItemFlag = 4;
        private const byte TorsoSplitFlag = 8;
        private const byte KnownFighterFlags = HasLowerSegmentsFlag | HasHeldItemFlag | HasDroppedItemFlag | TorsoSplitFlag;

        private const byte LimbOwnerRightFlag = 1;
        private const int LimbPartShift = 1;
        private const byte LimbPartMask = 7;
        private const byte LimbHasLowerFlag = 16;

        private const byte WallUpFlag = 1;
        private const int WallPhaseShift = 1;
        private const byte WallPhaseMask = 3;

        public static void Write(PacketWriter writer, IReadOnlyList<TimelineFrame> frames)
        {
            writer.WriteCount(frames.Count, ProtocolLimits.MaxTimelineFrames, "timeline frames");
            var predictor = new Predictor();
            int previousStep = 0;
            long previousMicroseconds = 0;
            foreach (TimelineFrame frame in frames)
            {
                writer.WriteVarInt((long)frame.Step - previousStep);
                writer.WriteVarInt(frame.Time.Microseconds - previousMicroseconds);
                previousStep = frame.Step;
                previousMicroseconds = frame.Time.Microseconds;
                WritePose(writer, frame.Pose, predictor);
            }
        }

        public static TimelineFrame[] Read(PacketReader reader)
        {
            int count = reader.ReadCount(ProtocolLimits.MaxTimelineFrames, "timeline frames", 2);
            var frames = new TimelineFrame[count];
            var predictor = new Predictor();
            long previousStep = 0;
            long previousMicroseconds = 0;
            for (int i = 0; i < count; i++)
            {
                long step = previousStep + reader.ReadVarInt();
                if (step < int.MinValue || step > int.MaxValue) throw PacketReader.Malformed($"Frame {i}'s step ({step}) does not fit.");
                long microseconds = previousMicroseconds + reader.ReadVarInt();
                previousStep = step;
                previousMicroseconds = microseconds;
                frames[i] = new TimelineFrame((int)step, new SimTime(microseconds), ReadPose(reader, predictor));
            }

            return frames;
        }

        private static void WritePose(PacketWriter writer, PoseSnapshot pose, Predictor predictor)
        {
            WriteFighter(writer, pose.Left, 0, predictor);
            WriteFighter(writer, pose.Right, ChannelsPerFighter, predictor);

            writer.WriteCount(pose.SeveredLimbs.Count, ProtocolLimits.MaxSeveredLimbs, "severed limbs in a frame");
            foreach (SeveredLimbPose limb in pose.SeveredLimbs)
            {
                writer.WriteByte((byte)((limb.Owner == Side.Right ? LimbOwnerRightFlag : 0) | ((int)limb.Part << LimbPartShift) |
                                        (limb.HasLower ? LimbHasLowerFlag : 0)));
                WriteBodyPose(writer, limb.Pose, LimbChannel(limb.Owner, limb.Part, 0), predictor);
                WriteBodyPose(writer, limb.Lower, LimbChannel(limb.Owner, limb.Part, 1), predictor);
            }

            WriteWall(writer, pose.LeftWall, WallChannelsStart, predictor);
            WriteWall(writer, pose.RightWall, WallChannelsStart + 1, predictor);
        }

        private static PoseSnapshot ReadPose(PacketReader reader, Predictor predictor)
        {
            var pose = new PoseSnapshot
            {
                Left = ReadFighter(reader, 0, predictor),
                Right = ReadFighter(reader, ChannelsPerFighter, predictor),
            };

            int limbs = reader.ReadCount(ProtocolLimits.MaxSeveredLimbs, "severed limbs in a frame", 2 * ValuesPerPose + 1);
            for (int i = 0; i < limbs; i++)
            {
                byte packed = reader.ReadByte();
                int part = (packed >> LimbPartShift) & LimbPartMask;
                if (part >= BodyPartCount || (packed & ~(LimbOwnerRightFlag | (LimbPartMask << LimbPartShift) | LimbHasLowerFlag)) != 0)
                    throw PacketReader.Malformed($"A severed limb is described by an impossible value ({packed}).");
                Side owner = (packed & LimbOwnerRightFlag) != 0 ? Side.Right : Side.Left;
                var limb = new SeveredLimbPose
                {
                    Owner = owner,
                    Part = (BodyPart)part,
                    HasLower = (packed & LimbHasLowerFlag) != 0,
                };
                limb.Pose = ReadBodyPose(reader, LimbChannel(owner, limb.Part, 0), predictor);
                limb.Lower = ReadBodyPose(reader, LimbChannel(owner, limb.Part, 1), predictor);
                pose.SeveredLimbs.Add(limb);
            }

            pose.LeftWall = ReadWall(reader, WallChannelsStart, predictor);
            pose.RightWall = ReadWall(reader, WallChannelsStart + 1, predictor);
            return pose;
        }

        private static void WriteFighter(PacketWriter writer, FighterPose fighter, int channel, Predictor predictor)
        {
            writer.WriteByte((byte)((fighter.HasLowerSegments ? HasLowerSegmentsFlag : 0) | (fighter.HasHeldItem ? HasHeldItemFlag : 0) |
                                    (fighter.HasDroppedItem ? HasDroppedItemFlag : 0) | (fighter.IsTorsoSplit ? TorsoSplitFlag : 0)));
            writer.WriteInt(fighter.SeveredParts);
            for (int i = 0; i < BodyPartCount; i++)
            {
                WriteBodyPose(writer, fighter.Parts[i], channel, predictor);
                channel += ValuesPerPose;
            }

            for (int i = 0; i < BodyPartCount; i++)
            {
                WriteBodyPose(writer, fighter.LowerSegments[i], channel, predictor);
                channel += ValuesPerPose;
            }

            WriteBodyPose(writer, fighter.HeldItem, channel, predictor);
            WriteBodyPose(writer, fighter.DroppedItem, channel + ValuesPerPose, predictor);
            WriteBodyPose(writer, fighter.TorsoTop, channel + 2 * ValuesPerPose, predictor);
        }

        private static FighterPose ReadFighter(PacketReader reader, int channel, Predictor predictor)
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
            for (int i = 0; i < BodyPartCount; i++)
            {
                fighter.Parts[i] = ReadBodyPose(reader, channel, predictor);
                channel += ValuesPerPose;
            }

            for (int i = 0; i < BodyPartCount; i++)
            {
                fighter.LowerSegments[i] = ReadBodyPose(reader, channel, predictor);
                channel += ValuesPerPose;
            }

            fighter.HeldItem = ReadBodyPose(reader, channel, predictor);
            fighter.DroppedItem = ReadBodyPose(reader, channel + ValuesPerPose, predictor);
            fighter.TorsoTop = ReadBodyPose(reader, channel + 2 * ValuesPerPose, predictor);
            return fighter;
        }

        private static void WriteWall(PacketWriter writer, ElectricWallPose wall, int channel, Predictor predictor)
        {
            writer.WriteByte((byte)((wall.IsUp ? WallUpFlag : 0) | ((int)wall.Phase << WallPhaseShift)));
            predictor.Write(writer, channel, PoseRounding.PositionSteps(wall.XUnits));
        }

        private static ElectricWallPose ReadWall(PacketReader reader, int channel, Predictor predictor)
        {
            byte flags = reader.ReadByte();
            int phase = (flags >> WallPhaseShift) & WallPhaseMask;
            if (phase > (int)ElectricWallPhase.Active || (flags & ~(WallUpFlag | (WallPhaseMask << WallPhaseShift))) != 0)
                throw PacketReader.Malformed($"A wall is described by an impossible value ({flags}).");
            return new ElectricWallPose
            {
                IsUp = (flags & WallUpFlag) != 0,
                Phase = (ElectricWallPhase)phase,
                XUnits = PoseRounding.Position(predictor.Read(reader, channel)),
            };
        }

        private static void WriteBodyPose(PacketWriter writer, BodyPose pose, int channel, Predictor predictor)
        {
            predictor.Write(writer, channel, PoseRounding.PositionSteps(pose.PositionUnits.X));
            predictor.Write(writer, channel + 1, PoseRounding.PositionSteps(pose.PositionUnits.Y));
            predictor.Write(writer, channel + 2, PoseRounding.RotationSteps(pose.RotationDegrees));
        }

        private static BodyPose ReadBodyPose(PacketReader reader, int channel, Predictor predictor)
        {
            float x = PoseRounding.Position(predictor.Read(reader, channel));
            float y = PoseRounding.Position(predictor.Read(reader, channel + 1));
            float rotation = PoseRounding.Rotation(predictor.Read(reader, channel + 2));
            return new BodyPose(new Vec2(x, y), rotation);
        }

        private static int LimbChannel(Side owner, BodyPart part, int segment) =>
            LimbChannelsStart + (((int)owner * BodyPartCount + (int)part) * 2 + segment) * ValuesPerPose;

        /// <summary>
        /// Predicts each value from its own last two (constant speed): nothing known gives 0, one value gives itself. The writer sends the
        /// difference and the reader adds it back, both updating the same history, so they always agree.
        /// </summary>
        private sealed class Predictor
        {
            private readonly long[] _last = new long[ChannelCount];
            private readonly long[] _previous = new long[ChannelCount];
            private readonly byte[] _known = new byte[ChannelCount];

            public void Write(PacketWriter writer, int channel, long value)
            {
                writer.WriteVarInt(value - Predict(channel));
                Push(channel, value);
            }

            public long Read(PacketReader reader, int channel)
            {
                long value = reader.ReadVarInt() + Predict(channel);
                Push(channel, value);
                return value;
            }

            private long Predict(int channel)
            {
                switch (_known[channel])
                {
                    case 0:
                        return 0;
                    case 1:
                        return _last[channel];
                    default:
                        return 2 * _last[channel] - _previous[channel];
                }
            }

            private void Push(int channel, long value)
            {
                _previous[channel] = _last[channel];
                _last[channel] = value;
                if (_known[channel] < 2) _known[channel]++;
            }
        }
    }
}
