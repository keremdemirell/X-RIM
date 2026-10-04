using System;
using NUnit.Framework;
using XRim.Presentation.UI;
using XRim.Rules.Planning;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>Every way the rules can refuse a command has a plain sentence for the HUD.</summary>
    public sealed class RejectionMessagesTests
    {
        [Test]
        public void EveryRefusalHasAnExplanation_AndNoneHasNone()
        {
            foreach (CommandRejection rejection in Enum.GetValues(typeof(CommandRejection)))
            {
                string text = RejectionMessages.Text(rejection, 1.5f);
                if (rejection == CommandRejection.None) Assert.That(text, Is.Empty);
                else Assert.That(text, Is.Not.Empty, rejection.ToString());
            }
        }

        [Test]
        public void TheLockoutMessagesNameTheTunableLength()
        {
            Assert.That(RejectionMessages.Text(CommandRejection.WeaponSwitchLockedOut, 1.5f), Does.Contain("1.5"));
            Assert.That(RejectionMessages.Text(CommandRejection.DrawingLockedOut, 2f), Does.Contain("2 s"));
        }
    }
}
