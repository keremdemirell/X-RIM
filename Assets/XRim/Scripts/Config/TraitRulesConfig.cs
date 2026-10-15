using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    /// <summary>The weapon traits' switches and numbers (GDD §15, Session 14): lifesteal and build-up, both off by default.</summary>
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Trait Rules", fileName = "TraitRules")]
    public sealed class TraitRulesConfig : SettingsConfig<TraitSettings>
    {
    }
}
