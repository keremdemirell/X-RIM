using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Path Rules", fileName = "PathRules")]
    public sealed class PathRulesConfig : SettingsConfig<PathSettings>
    {
    }
}
