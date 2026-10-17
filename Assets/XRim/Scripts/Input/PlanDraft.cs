using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;

namespace XRim.Input
{
    /// <summary>
    /// What this device has set so far this turn, kept from the commands the rules accepted: the weapon, the last stroke
    /// (raw, in the torso frame), the body move and Ready. The preview and the HUD read it, so the rules stay the only
    /// judge: a refused command changes nothing here except <see cref="LastRejection"/>.
    /// </summary>
    public sealed class PlanDraft
    {
        public WeaponId Weapon { get; private set; }

        /// <summary>The raw stroke last accepted, in the torso frame; <see cref="WeaponPath.Empty"/> when none (D5: a new stroke replaces it).</summary>
        public WeaponPath Stroke { get; private set; } = WeaponPath.Empty;

        public BodyMove BodyMove { get; private set; }
        public bool IsReady { get; private set; }

        /// <summary>
        /// The signature move set (GDD §8); empty when none is. Its pattern is the path, so <see cref="Stroke"/> is empty meanwhile; a
        /// stroke, clearing the path or switching weapon cancels it, as the rules do.
        /// </summary>
        public SignatureMoveId Signature { get; private set; }

        /// <summary>Why the last command was refused; <see cref="CommandRejection.None"/> when it was accepted.</summary>
        public CommandRejection LastRejection { get; private set; }

        /// <summary>How many commands the rules refused this turn, so a HUD can tell a new refusal from an old one.</summary>
        public int RejectionCount { get; private set; }

        /// <summary>A new planning phase: nothing is set but the weapon already in hand.</summary>
        public void Reset(WeaponId currentWeapon)
        {
            Weapon = currentWeapon;
            Stroke = WeaponPath.Empty;
            BodyMove = BodyMove.None;
            IsReady = false;
            Signature = default;
            LastRejection = CommandRejection.None;
            RejectionCount = 0;
        }

        public void Record(PlanningCommand command, CommandResult result)
        {
            LastRejection = result.Rejection;
            if (!result.Accepted)
            {
                RejectionCount++;
                return;
            }

            switch (command)
            {
                case SelectWeaponCommand select:
                    // Picking the weapon already held is not a switch: the path stays (GDD §6).
                    if (select.Weapon != Weapon)
                    {
                        Weapon = select.Weapon;
                        Stroke = WeaponPath.Empty; // GDD §6 (Decided): switching erases the drawn path
                        Signature = default;
                    }

                    break;
                case SetPathCommand path:
                    Stroke = path.Path;
                    Signature = default; // a redraw cancels a signature move (§8)
                    break;
                case ClearPathCommand _:
                    Stroke = WeaponPath.Empty;
                    Signature = default;
                    break;
                case UseSignatureCommand signature:
                    Signature = signature.Signature;
                    Stroke = WeaponPath.Empty; // the pattern is the path now
                    break;
                case SetBodyMoveCommand body:
                    BodyMove = body.Move;
                    break;
                case SetReadyCommand ready:
                    IsReady = ready.IsReady;
                    break;
            }
        }
    }
}
