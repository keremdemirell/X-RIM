using NUnit.Framework;
using XRim.Core;
using XRim.Input;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;

namespace XRim.Tests.EditMode.Input
{
    /// <summary>The draft mirrors what the rules accepted (GDD §6: switching erases the path; D5: a new stroke replaces the old).</summary>
    public sealed class PlanDraftTests
    {
        private static readonly WeaponPath Stroke = new WeaponPath(new[] { new Vec2(100f, 100f), new Vec2(300f, 100f) });
        private static readonly CommandResult Refused = CommandResult.Reject(CommandRejection.InkBudgetExceeded);

        private static PlanDraft Fresh()
        {
            var draft = new PlanDraft();
            draft.Reset(WeaponIds.Rapier);
            return draft;
        }

        [Test]
        public void ANewPlanningPhase_StartsWithOnlyTheWeaponInHand()
        {
            PlanDraft draft = Fresh();
            draft.Record(new SetPathCommand(Stroke), CommandResult.Ok);
            draft.Record(new SetBodyMoveCommand(BodyMove.Jump), CommandResult.Ok);
            draft.Record(new SetReadyCommand(true), CommandResult.Ok);

            draft.Reset(WeaponIds.Mace);

            Assert.That(draft.Weapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(draft.Stroke.Points, Is.Empty);
            Assert.That(draft.BodyMove, Is.EqualTo(BodyMove.None));
            Assert.That(draft.IsReady, Is.False);
            Assert.That(draft.LastRejection, Is.EqualTo(CommandRejection.None));
        }

        [Test]
        public void AcceptedCommands_AreRemembered_ANewStrokeReplacesTheOld()
        {
            PlanDraft draft = Fresh();
            var second = new WeaponPath(new[] { new Vec2(0f, 0f), new Vec2(50f, 0f) });

            draft.Record(new SetPathCommand(Stroke), CommandResult.Ok);
            draft.Record(new SetPathCommand(second), CommandResult.Ok);
            draft.Record(new SetBodyMoveCommand(BodyMove.Lunge), CommandResult.Ok);
            draft.Record(new SetReadyCommand(true), CommandResult.Ok);

            Assert.That(draft.Stroke, Is.SameAs(second));
            Assert.That(draft.BodyMove, Is.EqualTo(BodyMove.Lunge));
            Assert.That(draft.IsReady, Is.True);

            draft.Record(new SetReadyCommand(false), CommandResult.Ok);
            Assert.That(draft.IsReady, Is.False);
        }

        [Test]
        public void ARefusedCommand_ChangesNothing_ButIsRemembered()
        {
            PlanDraft draft = Fresh();
            draft.Record(new SetPathCommand(Stroke), CommandResult.Ok);

            draft.Record(new SetPathCommand(new WeaponPath(new[] { new Vec2(9f, 9f) })), Refused);
            draft.Record(new SetBodyMoveCommand(BodyMove.Crouch), Refused);
            draft.Record(new SetReadyCommand(true), Refused);

            Assert.That(draft.Stroke, Is.SameAs(Stroke));
            Assert.That(draft.BodyMove, Is.EqualTo(BodyMove.None));
            Assert.That(draft.IsReady, Is.False);
            Assert.That(draft.LastRejection, Is.EqualTo(CommandRejection.InkBudgetExceeded));

            draft.Record(new SetBodyMoveCommand(BodyMove.Crouch), CommandResult.Ok);
            Assert.That(draft.LastRejection, Is.EqualTo(CommandRejection.None), "the next accepted command clears it");
        }

        [Test]
        public void EveryRefusalIsCounted_AndANewPhaseStartsAtZero()
        {
            PlanDraft draft = Fresh();

            draft.Record(new SetReadyCommand(true), Refused);
            draft.Record(new SetReadyCommand(true), Refused);
            draft.Record(new SetReadyCommand(true), CommandResult.Ok);

            Assert.That(draft.RejectionCount, Is.EqualTo(2));

            draft.Reset(WeaponIds.Rapier);
            Assert.That(draft.RejectionCount, Is.Zero);
        }

        [Test]
        public void SwitchingTheWeapon_ErasesThePath_ButPickingTheSameOneDoesNot()
        {
            PlanDraft draft = Fresh();
            draft.Record(new SetPathCommand(Stroke), CommandResult.Ok);

            draft.Record(new SelectWeaponCommand(WeaponIds.Rapier), CommandResult.Ok);
            Assert.That(draft.Stroke, Is.SameAs(Stroke), "not a switch");

            draft.Record(new SelectWeaponCommand(WeaponIds.Mace), CommandResult.Ok);
            Assert.That(draft.Weapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(draft.Stroke.Points, Is.Empty, "§6 (Decided): switching erases the path");
        }

        [Test]
        public void ClearingThePath_EmptiesTheDraft()
        {
            PlanDraft draft = Fresh();
            draft.Record(new SetPathCommand(Stroke), CommandResult.Ok);

            draft.Record(new ClearPathCommand(), CommandResult.Ok);

            Assert.That(draft.Stroke.Points, Is.Empty);
        }

        // --- Signature moves (GDD §8, Session 15) --------------------------------------------------

        private static readonly SignatureMoveId Move = SignatureMoveIds.FiveFingerExplodingTouch;

        [Test]
        public void AnAcceptedMove_IsRemembered_InPlaceOfTheStroke()
        {
            PlanDraft draft = Fresh();
            draft.Record(new SetPathCommand(Stroke), CommandResult.Ok);

            draft.Record(new UseSignatureCommand(Move), CommandResult.Ok);

            Assert.That(draft.Signature, Is.EqualTo(Move));
            Assert.That(draft.Stroke.Points, Is.Empty, "the pattern is the path now");
        }

        [Test]
        public void ARefusedMove_ChangesNothing()
        {
            PlanDraft draft = Fresh();
            draft.Record(new SetPathCommand(Stroke), CommandResult.Ok);

            draft.Record(new UseSignatureCommand(Move), CommandResult.Reject(CommandRejection.SignatureNotCharged));

            Assert.That(draft.Signature.IsEmpty, Is.True);
            Assert.That(draft.Stroke, Is.SameAs(Stroke));
        }

        [Test]
        public void ARedrawClearingOrASwitch_CancelsTheMove_AsTheRulesDo()
        {
            PlanDraft draft = Fresh();

            draft.Record(new UseSignatureCommand(Move), CommandResult.Ok);
            draft.Record(new SetPathCommand(Stroke), CommandResult.Ok);
            Assert.That(draft.Signature.IsEmpty, Is.True, "a redraw");

            draft.Record(new UseSignatureCommand(Move), CommandResult.Ok);
            draft.Record(new ClearPathCommand(), CommandResult.Ok);
            Assert.That(draft.Signature.IsEmpty, Is.True, "clearing");

            draft.Record(new UseSignatureCommand(Move), CommandResult.Ok);
            draft.Record(new SelectWeaponCommand(WeaponIds.Rapier), CommandResult.Ok);
            Assert.That(draft.Signature, Is.EqualTo(Move), "the same weapon is not a switch");
            draft.Record(new SelectWeaponCommand(WeaponIds.Mace), CommandResult.Ok);
            Assert.That(draft.Signature.IsEmpty, Is.True, "a switch");

            draft.Record(new UseSignatureCommand(Move), CommandResult.Ok);
            draft.Record(new SetBodyMoveCommand(BodyMove.Lunge), CommandResult.Ok);
            Assert.That(draft.Signature, Is.EqualTo(Move), "a swipe keeps it");

            draft.Reset(WeaponIds.Rapier);
            Assert.That(draft.Signature.IsEmpty, Is.True, "a new planning phase");
        }
    }
}
