using XRim.Core;
using XRim.Rules;
using XRim.Rules.Planning;

namespace XRim.Tests.EditMode.Rules.Planning
{
    /// <summary>The same weapon tip for every weapon.</summary>
    internal sealed class FixedWeaponTipSource : IWeaponTipSource
    {
        private readonly Vec2 _tip;

        public FixedWeaponTipSource(Vec2 tip)
        {
            _tip = tip;
        }

        public Vec2 TipLocal(WeaponId weapon) => _tip;
    }
}
