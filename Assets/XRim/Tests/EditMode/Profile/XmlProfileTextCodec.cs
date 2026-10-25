using System;
using System.IO;
using System.Xml.Serialization;
using XRim.Profile;

namespace XRim.Tests.EditMode.Profile
{
    /// <summary>
    /// A text format for the engine-free Profile tests (the game's is JSON through Unity's JsonUtility, tested in the Editor): XML through
    /// the .NET serializer, which reads the same public fields.
    /// </summary>
    internal sealed class XmlProfileTextCodec : IProfileTextCodec
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(ProfileData));

        public string ToText(ProfileData data)
        {
            using (var writer = new StringWriter())
            {
                Serializer.Serialize(writer, data);
                return writer.ToString();
            }
        }

        public ProfileData FromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("the file is empty");
            try
            {
                using (var reader = new StringReader(text))
                {
                    return (ProfileData)Serializer.Deserialize(reader);
                }
            }
            catch (InvalidOperationException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }
    }
}
