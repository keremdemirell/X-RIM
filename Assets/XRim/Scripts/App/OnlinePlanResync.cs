using XRim.Core;
using XRim.Input;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;

namespace XRim.App
{
    /// <summary>
    /// Online (Session 19): when the server put this player's plan back to the one it holds (it refused a command the phone had accepted,
    /// or the player rejoined mid-planning), the HUD's record of the plan (<see cref="PlanDraft"/>: the weapon in the selector, the preview's
    /// stroke, the body move, Ready) is rebuilt to match, and a refusal shows its usual note.
    /// </summary>
    public static class OnlinePlanResync
    {
        /// <param name="stroke">The raw stroke behind the plan's path (empty when none is drawn or a signature move is set).</param>
        /// <param name="refusal">The server's refusal to show as a note; null for none (a rejoin).</param>
        public static void Apply(PlanDraft draft, TurnPlan plan, WeaponPath stroke, CommandRejection? refusal)
        {
            Guard.NotNull(draft, nameof(draft));
            Guard.NotNull(plan, nameof(plan));
            draft.Reset(plan.Weapon);
            if (plan.BodyMove != BodyMove.None) draft.Record(new SetBodyMoveCommand(plan.BodyMove), CommandResult.Ok);
            if (!plan.Signature.IsEmpty) draft.Record(new UseSignatureCommand(plan.Signature), CommandResult.Ok);
            else if (stroke != null && !stroke.IsEmpty) draft.Record(new SetPathCommand(stroke), CommandResult.Ok);
            if (plan.PressedReady) draft.Record(new SetReadyCommand(true), CommandResult.Ok);

            // A refused command changes nothing in the record but the note it leaves (the command itself does not matter).
            if (refusal.HasValue && refusal.Value != CommandRejection.None) draft.Record(new ClearPathCommand(), CommandResult.Reject(refusal.Value));
        }
    }
}
