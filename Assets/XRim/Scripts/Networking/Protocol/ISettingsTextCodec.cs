using XRim.Rules.Settings;
using XRim.Simulation;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// Writes the rules and simulation settings a match is played with as text and reads them back (Session 19). The server sends its
    /// snapshot in <see cref="MatchFoundMessage"/>, so the phone previews paths, ink and the loadout pick with exactly the server's numbers.
    /// The engine-free protocol only carries the text; Unity's serializer does the writing (XRim.Config), so a field added to a settings
    /// class travels without a codec change.
    /// </summary>
    public interface ISettingsTextCodec
    {
        string Write(RulesSettings rules, SimulationSettings simulation);

        /// <summary>Reads what <see cref="Write"/> wrote; a text it cannot read is a <see cref="System.FormatException"/>.</summary>
        void Read(string text, out RulesSettings rules, out SimulationSettings simulation);
    }
}
