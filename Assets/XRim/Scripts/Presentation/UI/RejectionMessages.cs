using System.Globalization;
using XRim.Rules.Planning;

namespace XRim.Presentation.UI
{
    /// <summary>Plain words for why the rules refused a command, for the HUD's short note.</summary>
    public static class RejectionMessages
    {
        public static string Text(CommandRejection rejection, float lockoutSeconds)
        {
            string lockout = lockoutSeconds.ToString("0.#", CultureInfo.InvariantCulture);
            switch (rejection)
            {
                case CommandRejection.None: return string.Empty;
                case CommandRejection.NotInPlanningPhase: return "Planning is over.";
                case CommandRejection.WeaponNotInLoadout: return "That weapon is not in your loadout.";
                case CommandRejection.WeaponSwitchLockedOut: return "Weapons are locked for the last " + lockout + " s of planning.";
                case CommandRejection.BodyMoveNotAllowed: return "You cannot make a body move this turn.";
                case CommandRejection.InkBudgetExceeded: return "This weapon has no ink.";
                case CommandRejection.InvalidPath: return "That stroke did not make a path.";
                case CommandRejection.SignatureUnavailable: return "No signature move is available.";
                case CommandRejection.SignatureNotCharged: return "Your signature move is not charged.";
                case CommandRejection.SignatureWrongWeapon: return "That signature move does not work with this weapon.";
                case CommandRejection.ReadyCancelNotAllowed: return "Ready cannot be cancelled now.";
                case CommandRejection.DrawingLockedOut: return "Drawing is locked for the last " + lockout + " s of planning.";
                case CommandRejection.AlreadyReady: return "You are Ready: cancel Ready to change your plan.";
                default: return rejection.ToString();
            }
        }
    }
}
