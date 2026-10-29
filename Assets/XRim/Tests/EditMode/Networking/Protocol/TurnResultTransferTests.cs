using System.Globalization;
using NUnit.Framework;
using XRim.Networking.Protocol;
using XRim.Simulation.Recording;
using XRim.Tests.EditMode.Simulation;
using XRim.Tests.EditMode.Simulation.Scenarios;

namespace XRim.Tests.EditMode.Networking.Protocol
{
    /// <summary>
    /// A turn's result reaches both players whole (Session 19, GDD §18 server-authoritative): every frame (rounded finely), every event and
    /// raw contact with its exact numbers, the exact final board and the report, and small enough to arrive quickly between Ready and
    /// playback.
    /// </summary>
    public sealed class TurnResultTransferTests
    {
        /// <summary>
        /// The most a full-length turn may take as it travels. About a fifth of a second on a slow 3G link (around 150 KB/s) and nothing on
        /// Wi-Fi; the synthetic turn measures well under it (see the test's message). Batch 5 measures real turns from the server.
        /// </summary>
        private const int FullLengthTurnBudgetBytes = 32 * 1024;

        [Test]
        public void ATurnWithEveryEvent_ArrivesWhole_FramesRounded()
        {
            TurnResult turn = ScenarioTestData.TurnWithEveryEvent();

            TurnResult arrived = RoundTrip(turn);

            Assert.That(TurnFingerprint.Of(arrived), Is.EqualTo(TurnFingerprint.Of(ProtocolTestData.WithFramesRounded(turn))));
        }

        [Test]
        public void AFullLengthTurn_ArrivesWhole_FramesRounded()
        {
            TurnResult turn = ProtocolTestData.FullLengthTurn();

            TurnResult arrived = RoundTrip(turn);

            Assert.That(arrived.Timeline.Frames.Count, Is.EqualTo(ProtocolTestData.FullLengthSteps + 1), "every frame, none dropped");
            Assert.That(TurnFingerprint.Of(arrived), Is.EqualTo(TurnFingerprint.Of(ProtocolTestData.WithFramesRounded(turn))));
        }

        [Test]
        public void TheFinalBoard_ArrivesExactly_NotRounded()
        {
            TurnResult turn = ProtocolTestData.FullLengthTurn();

            TurnResult arrived = RoundTrip(turn);

            Assert.That(TurnFingerprint.OfBoard(arrived.FinalBoard), Is.EqualTo(TurnFingerprint.OfBoard(turn.FinalBoard)));
        }

        [Test]
        public void AFullLengthTurn_FitsTheBudget()
        {
            TurnResult turn = ProtocolTestData.FullLengthTurn();

            int packed = ProtocolCodec.Encode(new TurnResolvedMessage(turn)).Length;
            int unpacked = ProtocolCodec.Encode(new TurnResolvedMessage(turn), false).Length;
            int plain = ProtocolTestData.PlainFrameBytes(turn);
            int smooth = ProtocolCodec.Encode(new TurnResolvedMessage(ProtocolTestData.FullLengthTurn(0f))).Length;

            Assert.That(packed, Is.LessThan(FullLengthTurnBudgetBytes));
            Assert.Pass(string.Format(CultureInfo.InvariantCulture,
                "Synthetic full-length turn ({0} frames, ten-body dummies, a flying limb, {1} events), every body wobbling up to {2} units at " +
                "random: {3:N0} bytes as sent, {4:N0} unpacked, {5:N0} for its frames as plain numbers. The same turn moving smoothly: {6:N0} bytes.",
                turn.Timeline.Frames.Count, turn.Timeline.Events.Count, ProtocolTestData.DefaultJitterUnits, packed, unpacked, plain, smooth));
        }

        private static TurnResult RoundTrip(TurnResult turn) =>
            ((TurnResolvedMessage)ProtocolCodec.Decode(ProtocolCodec.Encode(new TurnResolvedMessage(turn)))).Result;
    }
}
