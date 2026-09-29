using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    /// <summary>One weapon, shield or severed-limb club (GDD §6). Visual references are added here later; they never reach the rules.</summary>
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Weapon", fileName = "Weapon")]
    public sealed class WeaponDefinition : SettingsConfig<WeaponStats>
    {
    }
}
