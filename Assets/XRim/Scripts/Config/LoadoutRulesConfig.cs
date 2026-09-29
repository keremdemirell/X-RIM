using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Loadout Rules", fileName = "LoadoutRules")]
    public sealed class LoadoutRulesConfig : SettingsConfig<LoadoutSettings>
    {
    }
}
