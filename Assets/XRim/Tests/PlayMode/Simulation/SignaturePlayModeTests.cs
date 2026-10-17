using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XRim.Config;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Signature;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;
using Object = UnityEngine.Object;

namespace XRim.Tests.PlayMode.Simulation
{
    /// <summary>
    /// A signature move end to end on real Unity 2D physics (GDD §8, Session 15): chosen with a tap, validated, played as its pattern,
    /// revealed in the turn's result, spent, and its special property felt by the victim. Logs "[XRim signature] …" with what happened.
    /// </summary>
    public sealed class SignaturePlayModeTests
    {
        /// <summary>Pelvis to pelvis 600: an unarmed target well inside the rapier's reach (arm 240 + 400), as in the hit tests.</summary>
        private const float RapierRangeHalfGapUnits = 300f;

        private SimulationMode2D _previousMode;
        private ArenaSpace _space;
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private RulePolicies _policies;
        private Ragdoll _sixBodies;
        private Ragdoll _tenBodies;
        private readonly AimFromShoulderModel _aim = new AimFromShoulderModel();
        private readonly List<Unity2DPhysicsWorld> _worlds = new List<Unity2DPhysicsWorld>();

        [SetUp]
        public void SetUp()
        {
            _previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            _space = new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit);
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _policies = new RulePolicies();
            _sixBodies = BuildTemplate(RagdollSegmentation.SixBodies);
            _tenBodies = BuildTemplate(RagdollSegmentation.TenBodies);
            _worlds.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Unity2DPhysicsWorld world in _worlds)
            {
                Scene scene = world.Scene;
                world.Dispose();
                yield return new WaitUntil(() => !scene.isLoaded);
            }

            Object.DestroyImmediate(_sixBodies.gameObject);
            Object.DestroyImmediate(_tenBodies.gameObject);
            Physics2D.simulationMode = _previousMode;
        }

        private Ragdoll BuildTemplate(RagdollSegmentation segmentation)
        {
            Ragdoll ragdoll = PlaceholderRagdollBuilder.Build(new RagdollBuildSpec(_simulation.Ragdoll, _rules.Paths, segmentation, _space, _rules.Weapons, null));
            ragdoll.gameObject.SetActive(false);
            return ragdoll;
        }

        private Unity2DPhysicsWorld NewWorld()
        {
            var world = new Unity2DPhysicsWorld(_space, new RagdollPrefabSet(_sixBodies, _tenBodies));
            _worlds.Add(world);
            return world;
        }

        private static string Describe(TurnResult result)
        {
            List<HitLandedEvent> hits = result.Timeline.Events.OfType<HitLandedEvent>().ToList();
            if (hits.Count == 0) return "no hits";
            return string.Join("; ", hits.Select(hit => string.Format(CultureInfo.InvariantCulture, "{0} → {1} at {2:0.0} ms for {3:0.0}",
                hit.Hit.Attacker, hit.Hit.Part, hit.Time.Milliseconds, hit.Damage.HpDamage)));
        }

        private static void Report(string format, params object[] args) =>
            Debug.Log("[XRim signature] " + string.Format(CultureInfo.InvariantCulture, format, args));

        private static float PelvisX(PoseSnapshot pose, Side side) => pose.Get(side).Get(BodyPart.Torso).PositionUnits.X;

        /// <summary>
        /// The whole loop through the real authority and physics, from the starting board: the left player swipes a lunge and taps
        /// "5 Finger Exploding Touch"; the move is accepted, plays as its pattern, is revealed by name in the turn, and is spent (once per
        /// match: the next window has no charge on that side, while the other side keeps its own).
        /// </summary>
        [UnityTest]
        public IEnumerator AMatchsSignatureMove_IsChosenPlayedRevealedAndSpent()
        {
            WeaponId[] loadout = { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield };
            var setup = new MatchSetup(new PerSide<FighterSetup>(new FighterSetup(Handedness.Right, loadout), new FighterSetup(Handedness.Right, loadout)), 7u);
            var authority = new LocalTurnAuthority(setup, _rules, _simulation, _policies, new TurnSimulator(NewWorld(), _policies), new ManualClock(0.0));
            var windows = new List<PlanningWindow>();
            var results = new List<TurnResult>();
            authority.PlanningStarted += windows.Add;
            authority.TurnResolved += results.Add;

            authority.Start();
            Assert.That(windows[0].Signatures[Side.Left].CanUse, Is.True, "once per match: charged from turn 1");
            Assert.That(authority.Send(Side.Left, new SetBodyMoveCommand(BodyMove.Lunge)).Accepted, Is.True);
            Assert.That(authority.Send(Side.Left, new UseSignatureCommand(SignatureMoveIds.FiveFingerExplodingTouch)).Accepted, Is.True);
            authority.Send(Side.Right, new SetBodyMoveCommand(BodyMove.Crouch));
            authority.Send(Side.Left, new SetReadyCommand(true));
            authority.Send(Side.Right, new SetReadyCommand(true));
            authority.Tick();

            Assert.That(results, Has.Count.EqualTo(1), "the turn ran on real physics");
            TurnResult turn = results[0];
            float gripTravel = Vec2.Distance(turn.FinalBoard.Pose.Left.HeldItem.PositionUnits, windows[0].Board.Pose.Left.HeldItem.PositionUnits);
            Report("Turn 1 with the Exploding Touch and a lunge: {0}; the left grip travelled {1:0}.", Describe(turn), gripTravel);

            SignatureUsedEvent used = turn.Timeline.Events.OfType<SignatureUsedEvent>().Single();
            Assert.That(used.Side, Is.EqualTo(Side.Left));
            Assert.That(used.DisplayName, Is.EqualTo("5 Finger Exploding Touch"));
            Assert.That(used.Time, Is.EqualTo(SimTime.Zero), "revealed at the first instant of execution");
            Assert.That(turn.Timeline.Events.OfType<AttackStartedEvent>().Any(e => e.Side == Side.Left), Is.True, "the pattern drove the weapon");
            Assert.That(gripTravel, Is.GreaterThan(150f), "the weapon really went out along the pattern, carried by the lunge");
            Assert.That(turn.Report.ResolvedState.Fighters[Side.Left].SignatureUses, Is.EqualTo(1), "spent when the plan locked");

            PlanningWindow next = windows.Find(window => window.TurnIndex == 1);
            Assert.That(next, Is.Not.Null);
            Assert.That(next.Signatures[Side.Left].CanUse, Is.False, "once per match: gone");
            Assert.That(next.Signatures[Side.Right].CanUse, Is.True, "the other player still has theirs");
            Assert.That(authority.Send(Side.Left, new UseSignatureCommand(SignatureMoveIds.HeadImpactSimulation404)).Rejection,
                Is.EqualTo(CommandRejection.SignatureNotCharged));
            yield return null;
        }

        /// <summary>
        /// The special property on real physics: the same thrust along the Exploding Touch's pattern, once as the signature move and once
        /// drawn by hand, into an unarmed dummy 600 away. Both land; the move throws the victim about three times as far (E1 knockback ×3).
        /// </summary>
        [UnityTest]
        public IEnumerator TheExplodingTouch_ThrowsTheVictimFarther_ThanTheSameThrustDrawnByHand()
        {
            var board = new PoseSnapshot
            {
                Left = GuardStance.Create(new BodyPose(new Vec2(-RapierRangeHalfGapUnits, _simulation.Ragdoll.LegLengthUnits), 0f), Side.Left,
                    BodyPart.RightArm, _rules.FindWeapon(WeaponIds.Rapier), _simulation.Segmentation, _simulation.Ragdoll, _rules.Paths, _aim),
                Right = GuardStance.Create(new BodyPose(new Vec2(RapierRangeHalfGapUnits, _simulation.Ragdoll.LegLengthUnits), 0f), Side.Right,
                    BodyPart.RightArm, null, _simulation.Segmentation, _simulation.Ragdoll, _rules.Paths, _aim),
            };
            MatchState State() => new MatchState(
                new PerSide<FighterState>(
                    new FighterState(Handedness.Right, _rules.Damage.MaxHp, WeaponIds.Rapier),
                    new FighterState(Handedness.Right, _rules.Damage.MaxHp, WeaponIds.Rapier)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));

            Vec2 tip = new PoseWeaponTipLocator(_rules, _simulation).ForSide(new BoardSnapshot(State(), board), Side.Left).TipLocal(WeaponIds.Rapier);
            SignatureMoveStats touch = _rules.FindSignatureMove(SignatureMoveIds.FiveFingerExplodingTouch);
            WeaponPath pattern = new SignaturePathBuilder(_policies).Build(touch, tip, _rules.FindWeapon(WeaponIds.Rapier), _rules.Paths).Path;
            var idle = new TurnPlan(WeaponIds.Rapier, BodyMove.None, WeaponPath.Empty, default, true);

            TurnResult withMove = new TurnSimulator(NewWorld(), _policies).Simulate(new TurnInput(new BoardSnapshot(State(), board.Clone()),
                new PerSide<TurnPlan>(new TurnPlan(WeaponIds.Rapier, BodyMove.None, pattern, touch.MoveId, true), idle), _rules, _simulation));
            TurnResult byHand = new TurnSimulator(NewWorld(), _policies).Simulate(new TurnInput(new BoardSnapshot(State(), board.Clone()),
                new PerSide<TurnPlan>(new TurnPlan(WeaponIds.Rapier, BodyMove.None, pattern, default, true), idle), _rules, _simulation));

            float pushedByMove = PelvisX(withMove.FinalBoard.Pose, Side.Right) - RapierRangeHalfGapUnits;
            float pushedByHand = PelvisX(byHand.FinalBoard.Pose, Side.Right) - RapierRangeHalfGapUnits;
            float normalKnock = _rules.FindWeapon(WeaponIds.Rapier).Mass * _simulation.HitReaction.KnockbackUnitsPerWeaponMass;
            Report("Exploding Touch: {0}, victim pushed {1:0.0}. Drawn by hand: {2}, pushed {3:0.0}. One normal knockback: {4:0.0}.",
                Describe(withMove), pushedByMove, Describe(byHand), pushedByHand, normalKnock);

            Assert.That(withMove.Timeline.Events.OfType<HitLandedEvent>().Any(hit => hit.Hit.Attacker == Side.Left), Is.True,
                "the move lands: " + Describe(withMove));
            Assert.That(byHand.Timeline.Events.OfType<HitLandedEvent>().Any(hit => hit.Hit.Attacker == Side.Left), Is.True,
                "control: the same thrust lands: " + Describe(byHand));
            Assert.That(pushedByMove - pushedByHand, Is.GreaterThan(normalKnock),
                "the burst adds two more knockbacks (×3); allow for the physics settling");
            Assert.That(withMove.Timeline.Events.OfType<SignatureUsedEvent>().Count(), Is.EqualTo(1));
            Assert.That(byHand.Timeline.Events.OfType<SignatureUsedEvent>(), Is.Empty);
            yield return null;
        }
    }
}
