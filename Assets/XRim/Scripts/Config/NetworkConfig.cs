using UnityEngine;
using XRim.Networking;

namespace XRim.Config
{
    /// <summary>
    /// How online matches treat connections (Session 19): the port, how long the server waits for a dropped player (GDD §3, TBD) and for
    /// both playbacks, when a silent connection counts as lost, how often the phone pings and how early its countdown runs. Never a rule:
    /// a turn resolves the same however the connection behaves.
    /// </summary>
    [CreateAssetMenu(menuName = ConfigMenus.Client + "Network", fileName = "Network")]
    public sealed class NetworkConfig : SettingsConfig<NetworkSettings>
    {
    }
}
