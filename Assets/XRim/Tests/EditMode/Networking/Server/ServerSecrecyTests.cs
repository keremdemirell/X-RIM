using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Signature;
using XRim.Tests.EditMode.Networking.Protocol;

namespace XRim.Tests.EditMode.Networking.Server
{
    /// <summary>
    /// The opponent never receives a secret plan during planning (GDD §3, §8; Session 19): while one player switches weapon, swipes a body
    /// move, draws, plays a signature move and presses Ready, the other receives only public state (the weapon, Ready). The opponent's
    /// signature charge, the moves they brought and the match seed are blanked from boards and setups (designer 2026-10-25, Q3).
    /// </summary>
    public sealed class ServerSecrecyTests
    {
        [Test]
        public void TheOpponent_ReceivesOnlyPublicState_WhileAPlayerPlans()
        {
            var harness = new ServerHarness();
            (TestPlayer left, TestPlayer right) = harness.StartMatch();
            int before = right.Inbox.Count;

            left.Command(new SelectWeaponCommand(WeaponIds.Mace));
            left.Command(new SetBodyMoveCommand(BodyMove.Crouch));
            left.Command(new SetPathCommand(new WeaponPath(ProtocolTestData.Stroke())));
            left.Command(new UseSignatureCommand(SignatureMoveIds.FiveFingerExplodingTouch));
            left.Command(new SetReadyCommand(true));
            harness.Tick();
            ServerHarness.Read(left, right);

            var duringPlanning = right.Since(before);
            Assert.That(left.All<CommandAnswerMessage>().Count(answer => answer.Accepted), Is.EqualTo(5), "every secret input was set");
            Assert.That(duringPlanning.Select(message => message.Kind).Distinct(), Is.EquivalentTo(new[] { MessageKind.PublicState }),
                "the opponent receives no answer, no plan, nothing but public state");
            foreach (PublicStateMessage update in duringPlanning.OfType<PublicStateMessage>())
            {
                Assert.That(update.Side, Is.EqualTo(Side.Left));
                Assert.That(update.State.SignatureChargedVisible, Is.False, "a charged move is not shown by default (§8)");
            }

            PublicPlanningState last = duringPlanning.OfType<PublicStateMessage>().Last().State;
            Assert.That(last.Weapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(last.IsReady, Is.True);

            right.Command(new SetReadyCommand(true));
            harness.Tick();
            right.Read();
            Assert.That(right.All<TurnResolvedMessage>(), Has.Count.EqualTo(1), "the plan is revealed only inside the turn's result");
            Assert.That(harness.Simulator.Inputs[0].Plans.Left.Signature, Is.EqualTo(SignatureMoveIds.FiveFingerExplodingTouch));
            Assert.That(harness.Simulator.Inputs[0].Plans.Left.BodyMove, Is.EqualTo(BodyMove.Crouch));
        }

        [Test]
        public void TheOpponentsMovesBrought_AndTheMatchSeed_AreBlanked_YourOwnAreKept()
        {
            var harness = new ServerHarness();
            (TestPlayer left, TestPlayer right) = harness.StartMatch(new[] { SignatureMoveIds.FiveFingerExplodingTouch },
                new[] { SignatureMoveIds.WhiplashCalibrationSpin });

            var seenByRight = right.Last<MatchStartedMessage>().Setup;
            var seenByLeft = left.Last<MatchStartedMessage>().Setup;

            Assert.That(seenByRight.Fighters.Left.Signatures, Is.Empty);
            Assert.That(seenByRight.Fighters.Right.Signatures, Is.EqualTo(new[] { SignatureMoveIds.WhiplashCalibrationSpin }));
            Assert.That(seenByLeft.Fighters.Right.Signatures, Is.Empty);
            Assert.That(seenByLeft.Fighters.Left.Signatures, Is.EqualTo(new[] { SignatureMoveIds.FiveFingerExplodingTouch }));
            Assert.That(seenByRight.RandomSeed, Is.EqualTo(0u), "the seed decides the sudden-death coin toss");
            Assert.That(right.Last<PlanningStartedMessage>().OwnSignatures.Moves, Is.EqualTo(new[] { SignatureMoveIds.WhiplashCalibrationSpin }));
            Assert.That(left.Last<PlanningStartedMessage>().OwnSignatures.Moves, Is.EqualTo(new[] { SignatureMoveIds.FiveFingerExplodingTouch }));
        }

        [Test]
        public void TheOpponentsCharge_IsBlankedFromBoardsAndResults_UnlessItIsShown()
        {
            foreach (bool shown in new[] { false, true })
            {
                RulesSettings rules = GddStartingValues.CreateRulesSettings();
                rules.Signature.ChargeMode = SignatureChargeMode.KineticEnergyBar;
                rules.Signature.OpponentSeesCharged = shown;
                var harness = new ServerHarness(script: (turn, state) => state.Fighters.Left.SignatureCharge = 0.75f, rules: rules);
                (TestPlayer left, TestPlayer right) = harness.StartMatch();

                harness.PlayTurn(left, right);

                float seenByRight = right.Last<PlanningStartedMessage>().Board.State.Fighters.Left.SignatureCharge;
                float inResultForRight = right.Last<TurnResolvedMessage>().Result.FinalBoard.State.Fighters.Left.SignatureCharge;
                float inReportForRight = right.Last<TurnResolvedMessage>().Result.Report.ResolvedState.Fighters.Left.SignatureCharge;
                Assert.That(seenByRight, Is.EqualTo(shown ? 0.75f : 0f), $"shown {shown}: the planning board");
                Assert.That(inResultForRight, Is.EqualTo(shown ? 0.75f : 0f), $"shown {shown}: the result's board");
                Assert.That(inReportForRight, Is.EqualTo(shown ? 0.75f : 0f), $"shown {shown}: the result's report");
                Assert.That(left.Last<PlanningStartedMessage>().Board.State.Fighters.Left.SignatureCharge, Is.EqualTo(0.75f),
                    "a player always sees their own charge");
            }
        }
    }
}
