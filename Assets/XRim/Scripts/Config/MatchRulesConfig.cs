using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Match Rules", fileName = "MatchRules")]
    public sealed class MatchRulesConfig : SettingsConfig<MatchSettings>
    {
    }
}
