using UnityEngine;
using XRim.Bots;

namespace XRim.Config
{
    /// <summary>
    /// The developer and practice opponents (Session 16; GDD §17: practice and development only): which brain plays "vs bot" and each side
    /// of "bot vs bot", and how the heuristic bot plays. Bot behaviour, never a rule.
    /// </summary>
    [CreateAssetMenu(menuName = ConfigMenus.Client + "Bots", fileName = "Bots")]
    public sealed class BotConfig : SettingsConfig<BotSettings>
    {
    }
}
