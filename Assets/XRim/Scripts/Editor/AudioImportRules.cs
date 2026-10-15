using System;
using UnityEditor;
using UnityEngine;
using XRim.Presentation.Audio;

namespace XRim.Editor
{
    /// <summary>
    /// Session 13: every sound dropped into <c>Assets/XRim/Audio/Incoming/</c> is imported with the settings a phone game wants
    /// (<see cref="AudioImportProfile"/>): effects and grunts mono and decompressed when loaded (they start the instant they are
    /// triggered), the music loop stereo and streamed. Applied at every import of a file there, so the folder never needs hand settings
    /// (a setting changed by hand in the Inspector is put back at the next import).
    /// </summary>
    internal sealed class AudioImportRules : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(EditorPaths.AudioIncomingFolder + "/", StringComparison.OrdinalIgnoreCase)) return;
            if (!(assetImporter is AudioImporter importer)) return;

            AudioImportProfile profile = AudioImportProfile.For(assetPath);
            importer.forceToMono = profile.ForceToMono;
            importer.loadInBackground = profile.Streams;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = profile.Streams ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = profile.Quality;
            settings.preloadAudioData = !profile.Streams;
            importer.defaultSampleSettings = settings;
        }
    }
}
