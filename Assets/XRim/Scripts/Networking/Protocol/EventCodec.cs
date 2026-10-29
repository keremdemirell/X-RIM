using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;
using XRim.Rules.Status;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// A turn's events, every kind and every number exact, in their recorded order. Raw physics contacts travel too: the hit effects take a
    /// hit's impact point from the contact at the same instant (Session 13, A7), and the debug list shows them. An event type without a
    /// form here cannot be sent (<see cref="NotSupportedException"/>); <see cref="SupportedTypes"/> lets a test catch a new one.
    /// </summary>
    internal static class EventCodec
    {
        private const byte BodyMoveStarted = 1;
        private const byte AttackStarted = 2;
        private const byte SignatureUsed = 3;
        private const byte HitLanded = 4;
        private const byte WeaponClash = 5;
        private const byte ShieldBlock = 6;
        private const byte AttackInterrupted = 7;
        private const byte StatusApplied = 8;
        private const byte LimbSevered = 9;
        private const byte WeaponDropped = 10;
        private const byte FighterDied = 11;
        private const byte ElectricWall = 12;
        private const byte WeaponTraitHeal = 13;
        private const byte Contact = 14;

        /// <summary>The fewest bytes any event takes: its kind, its time and one more value.</summary>
        private const int MinBytesPerEvent = 3;

        public static IReadOnlyList<Type> SupportedTypes { get; } = new[]
        {
            typeof(BodyMoveStartedEvent), typeof(AttackStartedEvent), typeof(SignatureUsedEvent), typeof(HitLandedEvent),
            typeof(WeaponClashEvent), typeof(ShieldBlockEvent), typeof(AttackInterruptedEvent), typeof(StatusAppliedEvent),
            typeof(LimbSeveredEvent), typeof(WeaponDroppedEvent), typeof(FighterDiedEvent), typeof(ElectricWallEvent),
            typeof(WeaponTraitHealEvent), typeof(ContactEvent),
        };

        public static void Write(PacketWriter writer, IReadOnlyList<MatchEvent> events)
        {
            writer.WriteCount(events.Count, ProtocolLimits.MaxTurnEvents, "turn events");
            foreach (MatchEvent matchEvent in events)
            {
                WriteEvent(writer, matchEvent);
            }
        }

        public static MatchEvent[] Read(PacketReader reader)
        {
            var events = new MatchEvent[reader.ReadCount(ProtocolLimits.MaxTurnEvents, "turn events", MinBytesPerEvent)];
            for (int i = 0; i < events.Length; i++)
            {
                events[i] = ReadEvent(reader);
            }

            return events;
        }

        private static void WriteEvent(PacketWriter writer, MatchEvent matchEvent)
        {
            switch (matchEvent)
            {
                case BodyMoveStartedEvent move:
                    Begin(writer, BodyMoveStarted, move);
                    writer.WriteInt((int)move.Side);
                    writer.WriteInt((int)move.Move);
                    break;
                case AttackStartedEvent attack:
                    Begin(writer, AttackStarted, attack);
                    writer.WriteInt((int)attack.Side);
                    BoardCodec.WriteWeapon(writer, attack.Weapon);
                    writer.WriteInt((int)attack.Kind);
                    break;
                case SignatureUsedEvent signature:
                    Begin(writer, SignatureUsed, signature);
                    writer.WriteInt((int)signature.Side);
                    BoardCodec.WriteSignature(writer, signature.Move);
                    writer.WriteText(signature.DisplayName, ProtocolLimits.MaxTextBytes, "a signature move's name");
                    break;
                case HitLandedEvent hit:
                    Begin(writer, HitLanded, hit);
                    writer.WriteInt((int)hit.Hit.Attacker);
                    writer.WriteInt((int)hit.Hit.Part);
                    BoardCodec.WriteWeapon(writer, hit.Hit.Weapon);
                    writer.WriteVarInt(hit.Hit.Time.Microseconds);
                    writer.WriteBool(hit.Hit.IsOffHand);
                    writer.WriteFloat(hit.Damage.Damage);
                    writer.WriteFloat(hit.Damage.HpDamage);
                    writer.WriteFloat(hit.Damage.LimbDamage);
                    writer.WriteBool(hit.Damage.Stuns);
                    writer.WriteBool(hit.Damage.SeversLimb);
                    break;
                case WeaponClashEvent clash:
                    Begin(writer, WeaponClash, clash);
                    writer.WriteInt((int)clash.Result.Kind);
                    WriteOptionalSide(writer, clash.Result.Winner);
                    writer.WriteFloat(clash.Result.ContactAngleDegrees);
                    writer.WriteFloat(clash.Result.LeftPower);
                    writer.WriteFloat(clash.Result.RightPower);
                    BoardCodec.WriteVec2(writer, clash.ContactPointUnits);
                    break;
                case ShieldBlockEvent block:
                    Begin(writer, ShieldBlock, block);
                    writer.WriteInt((int)block.Blocker);
                    writer.WriteBool(block.Result.AttackStopped);
                    writer.WriteFloat(block.Result.DamageMultiplier);
                    writer.WriteBool(block.Result.ShieldHolderStaggered);
                    BoardCodec.WriteVec2(writer, block.ContactPointUnits);
                    writer.WriteFloat(block.ContactAngleDegrees);
                    writer.WriteFloat(block.FacePositionFraction);
                    break;
                case AttackInterruptedEvent interrupted:
                    Begin(writer, AttackInterrupted, interrupted);
                    writer.WriteInt((int)interrupted.Interrupted);
                    break;
                case StatusAppliedEvent status:
                    Begin(writer, StatusApplied, status);
                    writer.WriteInt((int)status.Side);
                    writer.WriteInt((int)status.Kind);
                    break;
                case LimbSeveredEvent sever:
                    Begin(writer, LimbSevered, sever);
                    writer.WriteInt((int)sever.Side);
                    writer.WriteInt((int)sever.Part);
                    break;
                case WeaponDroppedEvent drop:
                    Begin(writer, WeaponDropped, drop);
                    writer.WriteInt((int)drop.Side);
                    BoardCodec.WriteWeapon(writer, drop.Weapon);
                    break;
                case FighterDiedEvent death:
                    Begin(writer, FighterDied, death);
                    writer.WriteInt((int)death.Side);
                    break;
                case ElectricWallEvent wall:
                    Begin(writer, ElectricWall, wall);
                    writer.WriteInt((int)wall.Side);
                    writer.WriteInt((int)wall.Phase);
                    writer.WriteFloat(wall.HpDamage);
                    writer.WriteFloat(wall.PositionUnits);
                    writer.WriteBool(wall.IsKnockedInto);
                    break;
                case WeaponTraitHealEvent heal:
                    Begin(writer, WeaponTraitHeal, heal);
                    writer.WriteInt((int)heal.Side);
                    writer.WriteText(heal.TraitId, ProtocolLimits.MaxTextBytes, "a trait id");
                    writer.WriteFloat(heal.HpRecovered);
                    break;
                case ContactEvent contact:
                    Begin(writer, Contact, contact);
                    WriteContact(writer, contact.Contact);
                    break;
                default:
                    throw new NotSupportedException($"The event {matchEvent.GetType().Name} has no protocol form: add it to {nameof(EventCodec)}.");
            }
        }

        private static MatchEvent ReadEvent(PacketReader reader)
        {
            byte kind = reader.ReadByte();
            var time = new SimTime(reader.ReadVarInt());
            switch (kind)
            {
                case BodyMoveStarted:
                    return new BodyMoveStartedEvent(time, ReadSide(reader), reader.ReadEnum<BodyMove>("a body move"));
                case AttackStarted:
                    return new AttackStartedEvent(time, ReadSide(reader), BoardCodec.ReadWeapon(reader), reader.ReadEnum<WeaponKind>("a weapon kind"));
                case SignatureUsed:
                    return new SignatureUsedEvent(time, ReadSide(reader), BoardCodec.ReadSignature(reader),
                        reader.ReadText(ProtocolLimits.MaxTextBytes, "a signature move's name"));
                case HitLanded:
                {
                    var facts = new HitFacts(ReadSide(reader), reader.ReadEnum<BodyPart>("the part struck"), BoardCodec.ReadWeapon(reader),
                        new SimTime(reader.ReadVarInt()), reader.ReadBool("an off-hand hit"));
                    var damage = new DamageResult(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadBool("a stun"),
                        reader.ReadBool("a sever"));
                    return new HitLandedEvent(time, facts, damage);
                }
                case WeaponClash:
                {
                    var clashKind = reader.ReadEnum<ClashKind>("a clash kind");
                    Side? winner = ReadOptionalSide(reader);
                    var result = new ClashResult(clashKind, winner, reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                    return new WeaponClashEvent(time, result, BoardCodec.ReadVec2(reader));
                }
                case ShieldBlock:
                {
                    Side blocker = ReadSide(reader);
                    var result = new BlockResult(reader.ReadBool("an attack stopped"), reader.ReadFloat(), reader.ReadBool("a staggered holder"));
                    return new ShieldBlockEvent(time, blocker, result, BoardCodec.ReadVec2(reader), reader.ReadFloat(), reader.ReadFloat());
                }
                case AttackInterrupted:
                    return new AttackInterruptedEvent(time, ReadSide(reader));
                case StatusApplied:
                    return new StatusAppliedEvent(time, ReadSide(reader), reader.ReadEnum<StatusKind>("a status"));
                case LimbSevered:
                    return new LimbSeveredEvent(time, ReadSide(reader), reader.ReadEnum<BodyPart>("a severed limb"));
                case WeaponDropped:
                    return new WeaponDroppedEvent(time, ReadSide(reader), BoardCodec.ReadWeapon(reader));
                case FighterDied:
                    return new FighterDiedEvent(time, ReadSide(reader));
                case ElectricWall:
                    return new ElectricWallEvent(time, ReadSide(reader), reader.ReadEnum<ElectricWallPhase>("a wall phase"), reader.ReadFloat(),
                        reader.ReadFloat(), reader.ReadBool("knocked into the wall"));
                case WeaponTraitHeal:
                    return new WeaponTraitHealEvent(time, ReadSide(reader), reader.ReadText(ProtocolLimits.MaxTextBytes, "a trait id"),
                        reader.ReadFloat());
                case Contact:
                    return new ContactEvent(ReadContact(reader, time));
                default:
                    throw PacketReader.Malformed($"An event of an unknown kind ({kind}).");
            }
        }

        private static void Begin(PacketWriter writer, byte kind, MatchEvent matchEvent)
        {
            writer.WriteByte(kind);
            writer.WriteVarInt(matchEvent.Time.Microseconds);
        }

        private static void WriteContact(PacketWriter writer, TurnContact contact)
        {
            WriteTag(writer, contact.Facts.A);
            WriteTag(writer, contact.Facts.B);
            BoardCodec.WriteVec2(writer, contact.Facts.PointUnits);
            BoardCodec.WriteVec2(writer, contact.Facts.Normal);
            BoardCodec.WriteVec2(writer, contact.Facts.RelativeVelocityUnitsPerSecond);
            writer.WriteInt(contact.ReportedStep);
            WriteOptionalSide(writer, contact.WeaponSide);
            writer.WriteFloat(contact.PathDistanceUnits);
            writer.WriteFloat(contact.ContactAngleDegrees);
        }

        private static TurnContact ReadContact(PacketReader reader, SimTime time)
        {
            BodyTag a = ReadTag(reader);
            BodyTag b = ReadTag(reader);
            var facts = new ContactFacts(a, b, BoardCodec.ReadVec2(reader), BoardCodec.ReadVec2(reader), BoardCodec.ReadVec2(reader));
            int step = reader.ReadInt("a contact's step");
            Side? weaponSide = ReadOptionalSide(reader);
            return new TurnContact(facts, step, time, weaponSide, reader.ReadFloat(), reader.ReadFloat());
        }

        private static void WriteTag(PacketWriter writer, BodyTag tag)
        {
            WriteOptionalSide(writer, tag.Owner);
            writer.WriteInt((int)tag.Role);
            writer.WriteInt((int)tag.Part);
        }

        private static BodyTag ReadTag(PacketReader reader) =>
            new BodyTag(ReadOptionalSide(reader), reader.ReadEnum<BodyRole>("a body's role"), reader.ReadEnum<BodyPart>("a body part"));

        private static Side ReadSide(PacketReader reader) => reader.ReadEnum<Side>("a side");

        private static void WriteOptionalSide(PacketWriter writer, Side? side)
        {
            writer.WriteBool(side.HasValue);
            if (side.HasValue) writer.WriteInt((int)side.Value);
        }

        private static Side? ReadOptionalSide(PacketReader reader) => reader.ReadBool("a side given") ? ReadSide(reader) : (Side?)null;
    }
}
