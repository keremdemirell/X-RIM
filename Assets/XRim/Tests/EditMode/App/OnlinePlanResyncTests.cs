using NUnit.Framework;
using XRim.App;
using XRim.Core;
using XRim.Input;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;

namespace XRim.Tests.EditMode.App
{
    /// <summary>
    /// Online (Session 19): when the server puts the player's plan back to the one it holds, the HUD's record of the plan follows it, and a
    /// refusal leaves its usual note.
    /// </summary>
    public sealed class OnlinePlanResyncTests
    {
        private static readonly WeaponPath Stroke = new WeaponPath(new[] { new Vec2(100f, 90f), new Vec2(250f, 120f), new Vec2(400f, 100f) });

        [Test]
        public void TheRecord_BecomesTheServersPlan_AndTheRefusalShows()
        {
            var draft = new PlanDraft();
            draft.Reset(WeaponIds.Rapier);
            draft.Record(new SelectWeaponCommand(WeaponIds.Shield), CommandResult.Ok); // the phone allowed a late switch
            var held = new TurnPlan(WeaponIds.Rapier, BodyMove.Crouch, Stroke, default, true);

            OnlinePlanResync.Apply(draft, held, Stroke, CommandRejection.WeaponSwitchLockedOut);

            Assert.That(draft.Weapon, Is.EqualTo(WeaponIds.Rapier));
            Assert.That(draft.BodyMove, Is.EqualTo(BodyMove.Crouch));
            Assert.That(draft.Stroke.Points, Is.EqualTo(Stroke.Points));
            Assert.That(draft.IsReady, Is.True);
            Assert.That(draft.LastRejection, Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
            Assert.That(draft.RejectionCount, Is.EqualTo(1));
        }

        [Test]
        public void ASignatureMove_IsRecordedAsTheMove_NotAStroke()
        {
            var draft = new PlanDraft();
            var held = new TurnPlan(WeaponIds.Mace, BodyMove.None, Stroke, SignatureMoveIds.FiveFingerExplodingTouch, false);

            OnlinePlanResync.Apply(draft, held, WeaponPath.Empty, null);

            Assert.That(draft.Signature, Is.EqualTo(SignatureMoveIds.FiveFingerExplodingTouch));
            Assert.That(draft.Stroke.IsEmpty, Is.True);
            Assert.That(draft.RejectionCount, Is.EqualTo(0), "a rejoin leaves no note");
        }
    }
}
