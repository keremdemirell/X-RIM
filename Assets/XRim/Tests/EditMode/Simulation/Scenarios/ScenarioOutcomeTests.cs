using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Status;
using XRim.Simulation.Recording;
using XRim.Simulation.Scenarios;

namespace XRim.Tests.EditMode.Simulation.Scenarios
{
    /// <summary>
    /// What a saved scenario remembers of its turn (Session 16): every rules event in order (not the raw contacts) and how each dummy
    /// ended; and how a replay is compared with it, so a regression test says exactly what changed.
    /// </summary>
    public sealed class ScenarioOutcomeTests
    {
        [Test]
        public void Of_KeepsEveryRulesEventInOrder_AndLeavesOutTheRawContacts()
        {
            ScenarioOutcome outcome = ScenarioOutcome.Of(ScenarioTestData.TurnWithEveryEvent());

            Assert.That(outcome.Events.Select(e => e.Kind), Is.EqualTo(new[]
            {
                ScenarioEventKind.BodyMoveStarted, ScenarioEventKind.AttackStarted, ScenarioEventKind.SignatureUsed,
                ScenarioEventKind.WeaponClash, ScenarioEventKind.StatusApplied, ScenarioEventKind.ShieldBlock, ScenarioEventKind.HitLanded,
                ScenarioEventKind.AttackInterrupted, ScenarioEventKind.LimbSevered, ScenarioEventKind.WeaponDropped,
                ScenarioEventKind.ElectricWall, ScenarioEventKind.WeaponTraitHeal, ScenarioEventKind.FighterDied,
            }));
            Assert.That(outcome.EndReason, Is.EqualTo(TurnEndReason.HardCap));
            Assert.That(outcome.DurationMicroseconds, Is.EqualTo(1_000_000L));
        }

        [Test]
        public void Of_RecordsTheFactsOfEachEvent()
        {
            List<ScenarioEventRecord> events = ScenarioOutcome.Of(ScenarioTestData.TurnWithEveryEvent()).Events;

            ScenarioEventRecord hit = events.Single(e => e.Kind == ScenarioEventKind.HitLanded);
            Assert.That(hit.Side, Is.EqualTo(Side.Left));
            Assert.That(hit.Part, Is.EqualTo(BodyPart.RightArm));
            Assert.That(hit.Id, Is.EqualTo("rapier"));
            Assert.That(hit.Amount, Is.EqualTo(6.4f));
            Assert.That(hit.SecondAmount, Is.EqualTo(6.4f));
            Assert.That(hit.TimeMicroseconds, Is.EqualTo(120_500L));
            Assert.That(hit.Summary, Does.Contain("Left hits Right's RightArm with rapier"));

            ScenarioEventRecord clash = events.Single(e => e.Kind == ScenarioEventKind.WeaponClash);
            Assert.That((ClashKind)clash.Detail, Is.EqualTo(ClashKind.CrushThrough));
            Assert.That(clash.Flag, Is.True);
            Assert.That(clash.Side, Is.EqualTo(Side.Right));
            Assert.That(clash.Amount, Is.EqualTo(75f));

            ScenarioEventRecord block = events.Single(e => e.Kind == ScenarioEventKind.ShieldBlock);
            Assert.That(block.Side, Is.EqualTo(Side.Left));
            Assert.That(block.Flag, Is.False, "a partial block");
            Assert.That(block.SecondAmount, Is.EqualTo(0.5f));

            ScenarioEventRecord wall = events.Single(e => e.Kind == ScenarioEventKind.ElectricWall);
            Assert.That((ElectricWallPhase)wall.Detail, Is.EqualTo(ElectricWallPhase.Active));
            Assert.That(wall.Amount, Is.EqualTo(10f));
            Assert.That(wall.SecondAmount, Is.EqualTo(-762.5f));

            Assert.That((StatusKind)events.Single(e => e.Kind == ScenarioEventKind.StatusApplied).Detail, Is.EqualTo(StatusKind.Staggered));
            Assert.That(events.Single(e => e.Kind == ScenarioEventKind.SignatureUsed).Id, Is.EqualTo("head_impact_simulation_404"));
            Assert.That(events.Single(e => e.Kind == ScenarioEventKind.LimbSevered).Part, Is.EqualTo(BodyPart.RightArm));
            Assert.That(events.Single(e => e.Kind == ScenarioEventKind.WeaponTraitHeal).Amount, Is.EqualTo(4.5f));
        }

        [Test]
        public void Of_RecordsHowEachDummyEnded()
        {
            TurnResult turn = ScenarioTestData.TurnWithEveryEvent();

            ScenarioOutcome outcome = ScenarioOutcome.Of(turn);

            Assert.That(outcome.Left.Hp, Is.EqualTo(1f));
            Assert.That(outcome.Left.IsStunned, Is.True);
            Assert.That(outcome.Left.IsStaggered, Is.True);
            Assert.That(outcome.Left.Severed[(int)BodyPart.RightArm], Is.True);
            Assert.That(outcome.Left.LimbDamage[(int)BodyPart.LeftLeg], Is.EqualTo(12.3f));
            Assert.That(outcome.Left.CurrentWeapon, Is.EqualTo("severed_limb"));
            Assert.That(outcome.Left.PelvisUnits, Is.EqualTo(turn.FinalBoard.Pose.Left.Get(BodyPart.Torso).PositionUnits));
            Assert.That(outcome.Right.Severed[(int)BodyPart.LeftLeg], Is.True);
            Assert.That(outcome.Right.IsStaggered, Is.False);
        }

        [Test]
        public void Differences_TheSameOutcome_HasNone()
        {
            ScenarioOutcome recorded = Recorded();

            Assert.That(ScenarioComparison.Differences(recorded, Recorded(), ScenarioTolerance.Exact), Is.Empty);
        }

        [Test]
        public void Differences_DriftWithinTheTolerance_IsTheSameTurn()
        {
            ScenarioOutcome replayed = Recorded();
            HitOf(replayed).TimeMicroseconds += ScenarioTolerance.DefaultTimeMicroseconds;
            HitOf(replayed).Amount += 0.4f;
            replayed.Left.Hp += 0.4f;
            replayed.Left.PelvisUnits += new Vec2(3f, 0f);

            Assert.That(ScenarioComparison.Differences(Recorded(), replayed, ScenarioTolerance.Default), Is.Empty);
        }

        [Test]
        public void Differences_AHitBeyondTheTimeTolerance_IsNamed()
        {
            ScenarioOutcome replayed = Recorded();
            HitOf(replayed).TimeMicroseconds += ScenarioTolerance.DefaultTimeMicroseconds + 1L;

            List<string> differences = ScenarioComparison.Differences(Recorded(), replayed, ScenarioTolerance.Default);

            Assert.That(differences, Has.Count.EqualTo(1));
            Assert.That(differences[0], Does.Contain("Left hits Right's RightArm"));
        }

        [Test]
        public void Differences_AHitOnAnotherPart_IsADifferentEvent()
        {
            ScenarioOutcome replayed = Recorded();
            HitOf(replayed).Part = BodyPart.Head;

            List<string> differences = ScenarioComparison.Differences(Recorded(), replayed, ScenarioTolerance.Default);

            Assert.That(differences.Any(d => d.Contains("Head") && d.Contains("RightArm")), Is.True, string.Join("\n", differences));
        }

        [Test]
        public void Differences_AMissingEvent_IsNamed()
        {
            ScenarioOutcome replayed = Recorded();
            replayed.Events.RemoveAt(replayed.Events.Count - 1);

            List<string> differences = ScenarioComparison.Differences(Recorded(), replayed, ScenarioTolerance.Default);

            Assert.That(differences.Any(d => d.Contains("did not happen") && d.Contains("Right dies")), Is.True, string.Join("\n", differences));
        }

        [Test]
        public void Differences_AnotherEnding_IsNamed()
        {
            ScenarioOutcome replayed = Recorded();
            replayed.Right.IsDead = false;
            replayed.Right.Hp = 12f;
            replayed.Left.Severed[(int)BodyPart.LeftLeg] = true;

            List<string> differences = ScenarioComparison.Differences(Recorded(), replayed, ScenarioTolerance.Default);

            Assert.That(differences.Any(d => d.Contains("Right survived")), Is.True, string.Join("\n", differences));
            Assert.That(differences.Any(d => d.Contains("Right ended on 12 HP")), Is.True, string.Join("\n", differences));
            Assert.That(differences.Any(d => d.Contains("Left's LeftLeg was severed")), Is.True, string.Join("\n", differences));
        }

        [Test]
        public void Differences_ExactTolerance_CatchesTheSmallestDrift()
        {
            ScenarioOutcome replayed = Recorded();
            HitOf(replayed).TimeMicroseconds += 1L;

            Assert.That(ScenarioComparison.Differences(Recorded(), replayed, ScenarioTolerance.Exact), Is.Not.Empty);
        }

        private static ScenarioOutcome Recorded()
        {
            ScenarioOutcome outcome = ScenarioOutcome.Of(ScenarioTestData.TurnWithEveryEvent());
            outcome.Right.IsDead = true;
            outcome.Right.Hp = 0f;
            return outcome;
        }

        private static ScenarioEventRecord HitOf(ScenarioOutcome outcome) => outcome.Events.Single(e => e.Kind == ScenarioEventKind.HitLanded);
    }
}
