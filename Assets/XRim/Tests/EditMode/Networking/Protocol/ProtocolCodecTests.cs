using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Rules.Planning;
using XRim.Rules.Paths;
using XRim.Tests.EditMode.Simulation;

namespace XRim.Tests.EditMode.Networking.Protocol
{
    /// <summary>
    /// Every message survives the trip to bytes and back (Session 19), packed or not, and a message that cannot be trusted is refused with
    /// the reason: another protocol version, cut short, too large (as sent, as a list, or once unpacked), an unknown kind, an impossible
    /// value or bytes left over.
    /// </summary>
    public sealed class ProtocolCodecTests
    {
        private static IEnumerable<TestCaseData> Messages() =>
            ProtocolTestData.OneOfEachKind().Select(message => new TestCaseData(message).SetName(NameOf(message)));

        private static string NameOf(NetMessage message) =>
            message is PlanningCommandMessage command ? $"RoundTrip_{message.Kind}_{command.Command.GetType().Name}" : $"RoundTrip_{message.Kind}";

        [TestCaseSource(nameof(Messages))]
        public void EveryMessage_ComesBackTheSame_PackedAndUnpacked(NetMessage message)
        {
            foreach (bool packing in new[] { true, false })
            {
                NetMessage copy = ProtocolCodec.Decode(ProtocolCodec.Encode(message, packing));

                Assert.That(copy.GetType(), Is.EqualTo(message.GetType()));
                Assert.That(Fingerprint(copy), Is.EqualTo(Fingerprint(Expected(message))), $"packing {packing}");
            }
        }

        [Test]
        public void EveryKind_HasASample()
        {
            var kinds = new HashSet<MessageKind>(ProtocolTestData.OneOfEachKind().Select(message => message.Kind));

            foreach (MessageKind kind in Enum.GetValues(typeof(MessageKind)))
            {
                Assert.That(kinds, Does.Contain(kind), $"{kind} has no sample message, so no round-trip test.");
            }
        }

        [Test]
        public void BoardsInMessages_ComeBackExactly()
        {
            foreach (NetMessage message in ProtocolTestData.OneOfEachKind())
            {
                NetMessage copy = ProtocolCodec.Decode(ProtocolCodec.Encode(message));
                switch (message)
                {
                    case MatchStartedMessage started:
                        Assert.That(TurnFingerprint.OfBoard(((MatchStartedMessage)copy).InitialBoard), Is.EqualTo(TurnFingerprint.OfBoard(started.InitialBoard)));
                        break;
                    case PlanningStartedMessage planning:
                        Assert.That(TurnFingerprint.OfBoard(((PlanningStartedMessage)copy).Board), Is.EqualTo(TurnFingerprint.OfBoard(planning.Board)));
                        break;
                    case TurnResolvedMessage resolved:
                        Assert.That(TurnFingerprint.OfBoard(((TurnResolvedMessage)copy).Result.FinalBoard),
                            Is.EqualTo(TurnFingerprint.OfBoard(resolved.Result.FinalBoard)));
                        break;
                }
            }
        }

        [Test]
        public void AStroke_TravelsAsItsRawPoints_Exactly()
        {
            Vec2[] stroke = ProtocolTestData.Stroke(ProtocolLimits.MaxStrokePoints);
            var message = new PlanningCommandMessage(1, 0, new SetPathCommand(new WeaponPath(stroke)));

            var copy = (PlanningCommandMessage)ProtocolCodec.Decode(ProtocolCodec.Encode(message));

            Assert.That(((SetPathCommand)copy.Command).Path.Points, Is.EqualTo(stroke));
        }

        [Test]
        public void ALargeBody_IsPacked_ASmallOneIsNot()
        {
            byte[] turn = ProtocolCodec.Encode(new TurnResolvedMessage(ProtocolTestData.FullLengthTurn()));
            byte[] ready = ProtocolCodec.Encode(new PlanningCommandMessage(1, 0, new SetReadyCommand(true)));

            Assert.That(turn[3], Is.EqualTo(1), "the turn's result is packed");
            Assert.That(ready[3], Is.EqualTo(0), "a few bytes are never packed");
        }

        // --- Refusals ---------------------------------------------------------------------------

        [Test]
        public void AnotherVersion_IsRefused()
        {
            byte[] bytes = ProtocolCodec.Encode(new PlanningLockedMessage(3));
            bytes[0] = (byte)(ProtocolCodec.CurrentVersion + 1);

            Assert.That(Refusal(bytes), Is.EqualTo(ProtocolError.VersionMismatch));
        }

        [Test]
        public void AMessageCutShort_IsRefusedAsTruncated_AtEveryLength()
        {
            foreach (NetMessage message in ProtocolTestData.OneOfEachKind().Where(m => !(m is TurnResolvedMessage)))
            {
                byte[] bytes = ProtocolCodec.Encode(message, false);
                for (int length = 0; length < bytes.Length; length++)
                {
                    Assert.That(Refusal(bytes.Take(length).ToArray()), Is.EqualTo(ProtocolError.Truncated), $"{NameOf(message)} cut to {length} bytes");
                }
            }
        }

        [Test]
        public void APackedMessageCutShort_IsRefused_OrComesBackWhole_NeverDifferent()
        {
            var message = new TurnResolvedMessage(ProtocolTestData.FullLengthTurn());
            byte[] bytes = ProtocolCodec.Encode(message);
            string whole = Fingerprint(Expected(message));

            for (int length = 0; length < bytes.Length; length += length < 64 ? 1 : 97)
            {
                NetMessage copy;
                try
                {
                    copy = ProtocolCodec.Decode(bytes.Take(length).ToArray());
                }
                catch (ProtocolException)
                {
                    continue;
                }

                // Deflate's last bits only mark its end, so a cut there can still hold the whole message: it must then be the same one.
                Assert.That(Fingerprint(copy), Is.EqualTo(whole), $"cut to {length} of {bytes.Length} bytes");
            }

            Assert.Throws<ProtocolException>(() => ProtocolCodec.Decode(bytes.Take(bytes.Length / 2).ToArray()), "cut in half");
        }

        [Test]
        public void BytesLeftOver_AreRefused()
        {
            byte[] bytes = ProtocolCodec.Encode(new PlanningLockedMessage(3)).Concat(new byte[] { 0 }).ToArray();

            Assert.That(Refusal(bytes), Is.EqualTo(ProtocolError.Malformed));
        }

        [Test]
        public void AnUnknownKind_IsRefused()
        {
            byte[] bytes = ProtocolCodec.Encode(new PlanningLockedMessage(3));
            bytes[2] = 99;

            Assert.That(Refusal(bytes), Is.EqualTo(ProtocolError.UnknownKind));
        }

        [Test]
        public void UnknownHeaderFlags_AreRefused()
        {
            byte[] bytes = ProtocolCodec.Encode(new PlanningLockedMessage(3));
            bytes[3] = 4;

            Assert.That(Refusal(bytes), Is.EqualTo(ProtocolError.Malformed));
        }

        [Test]
        public void AMessageLargerThanTheLimit_IsRefusedUnread()
        {
            Assert.That(Refusal(new byte[ProtocolLimits.MaxMessageBytes + 1]), Is.EqualTo(ProtocolError.TooLarge));
        }

        [Test]
        public void AStrokeOverTheLimit_IsRefused_WhenReadAndWhenWritten()
        {
            // A planning command: its number, its turn, the kind "set path", then a count one past the limit.
            byte[] bytes = ProtocolTestData.Message(MessageKind.PlanningCommand, ProtocolTestData.Concat(
                ProtocolTestData.VarInt(1), ProtocolTestData.VarInt(0), new byte[] { 3 }, ProtocolTestData.VarUInt(ProtocolLimits.MaxStrokePoints + 1)));
            var tooLong = new PlanningCommandMessage(1, 0, new SetPathCommand(new WeaponPath(ProtocolTestData.Stroke(ProtocolLimits.MaxStrokePoints + 1))));

            Assert.That(Refusal(bytes), Is.EqualTo(ProtocolError.TooLarge));
            Assert.Throws<ArgumentException>(() => ProtocolCodec.Encode(tooLong));
        }

        [Test]
        public void AListLongerThanTheBytesLeft_IsRefusedBeforeAnythingIsAllocated()
        {
            // A stroke said to hold 4000 points, with none following.
            byte[] bytes = ProtocolTestData.Message(MessageKind.PlanningCommand, ProtocolTestData.Concat(
                ProtocolTestData.VarInt(1), ProtocolTestData.VarInt(0), new byte[] { 3 }, ProtocolTestData.VarUInt(4000)));

            Assert.That(Refusal(bytes), Is.EqualTo(ProtocolError.Truncated));
        }

        [Test]
        public void AHugeTimelineCount_IsRefused()
        {
            // A turn's result: turn index, end reason, then two billion frames.
            byte[] bytes = ProtocolTestData.Message(MessageKind.TurnResolved, ProtocolTestData.Concat(
                ProtocolTestData.VarInt(0), ProtocolTestData.VarInt(0), ProtocolTestData.VarUInt(2_000_000_000)));

            Assert.That(Refusal(bytes), Is.EqualTo(ProtocolError.TooLarge));
        }

        [Test]
        public void AValueNoFieldCanHold_IsRefused()
        {
            // A public state for side 9; then Ready given as 2.
            byte[] badSide = ProtocolTestData.Message(MessageKind.PublicState, ProtocolTestData.Concat(
                ProtocolTestData.VarInt(9), ProtocolTestData.VarUInt(0), new byte[] { 0, 0 }));
            byte[] badReady = ProtocolTestData.Message(MessageKind.PublicState, ProtocolTestData.Concat(
                ProtocolTestData.VarInt(0), ProtocolTestData.VarUInt(0), new byte[] { 2, 0 }));

            Assert.That(Refusal(badSide), Is.EqualTo(ProtocolError.Malformed));
            Assert.That(Refusal(badReady), Is.EqualTo(ProtocolError.Malformed));
        }

        [Test]
        public void AnUnknownCommand_IsRefused()
        {
            byte[] bytes = ProtocolTestData.Message(MessageKind.PlanningCommand, ProtocolTestData.Concat(
                ProtocolTestData.VarInt(1), ProtocolTestData.VarInt(0), new byte[] { 77 }));

            Assert.That(Refusal(bytes), Is.EqualTo(ProtocolError.Malformed));
        }

        [Test]
        public void ABodyDeclaringMoreThanTheLimit_IsRefusedBeforeUnpacking()
        {
            byte[] packed = ProtocolTestData.Concat(ProtocolTestData.VarUInt(ProtocolLimits.MaxUnpackedBytes + 1), Deflated(1024));

            Assert.That(Refusal(PackedMessage(packed)), Is.EqualTo(ProtocolError.TooLarge));
        }

        [Test]
        public void ABodyThatUnpacksBeyondWhatItDeclares_IsRefused_WithoutUnpackingItAll()
        {
            byte[] bomb = Deflated(9 << 20);
            byte[] packed = ProtocolTestData.Concat(ProtocolTestData.VarUInt(1024), bomb);

            Assert.That(bomb.Length, Is.LessThan(ProtocolLimits.MaxMessageBytes), "small as sent");
            Assert.That(Refusal(PackedMessage(packed)), Is.EqualTo(ProtocolError.Malformed));
        }

        [Test]
        public void ABodyThatUnpacksShortOfWhatItDeclares_IsRefused()
        {
            byte[] packed = ProtocolTestData.Concat(ProtocolTestData.VarUInt(4096), Deflated(1024));

            Assert.That(Refusal(PackedMessage(packed)), Is.EqualTo(ProtocolError.Truncated));
        }

        [Test]
        public void ADamagedPackedBody_IsRefused()
        {
            byte[] bytes = ProtocolTestData.Message(MessageKind.TurnResolved, Enumerable.Range(0, 200).Select(i => (byte)(i * 37 + 11)).ToArray());
            bytes[3] = 1;

            Assert.Throws<ProtocolException>(() => ProtocolCodec.Decode(bytes));
        }

        // --- Helpers ----------------------------------------------------------------------------

        /// <summary>A turn's frames arrive rounded; everything else in a message arrives exactly.</summary>
        private static NetMessage Expected(NetMessage message) =>
            message is TurnResolvedMessage resolved ? new TurnResolvedMessage(ProtocolTestData.WithFramesRounded(resolved.Result)) : message;

        private static string Fingerprint(NetMessage message) =>
            message is TurnResolvedMessage resolved ? TurnFingerprint.Of(resolved.Result) : TurnFingerprint.OfValue(message);

        private static ProtocolError Refusal(byte[] bytes) => Assert.Throws<ProtocolException>(() => ProtocolCodec.Decode(bytes)).Error;

        private static byte[] PackedMessage(byte[] packedBody)
        {
            byte[] bytes = ProtocolTestData.Message(MessageKind.TurnResolved, packedBody);
            bytes[3] = 1;
            return bytes;
        }

        /// <summary>This many zero bytes, deflated: tiny as sent, large once unpacked.</summary>
        private static byte[] Deflated(int zeroBytes)
        {
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
                {
                    var zeros = new byte[64 * 1024];
                    for (int left = zeroBytes; left > 0; left -= zeros.Length) deflate.Write(zeros, 0, Math.Min(left, zeros.Length));
                }

                return output.ToArray();
            }
        }
    }
}
