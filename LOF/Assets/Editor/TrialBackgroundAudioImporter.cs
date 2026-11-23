using UnityEditor;
using UnityEngine;

namespace AFKTrial.Editor
{
    // Keep the long ambience compressed and streamed rather than decoding it all on startup.
    internal sealed class TrialBackgroundAudioImporter : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (assetPath != "Assets/Resources/Audio/Background.wav") return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = UnityEngine.AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .6f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
        }
    }
}
