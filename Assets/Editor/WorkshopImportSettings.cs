using System;
using UnityEditor;
using UnityEngine;

namespace VRPractice.Editor
{
    // Import scale is applied to asset data; all placed model roots keep scale (1,1,1).
    public sealed class WorkshopImportSettings : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Models/", StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Low;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = assetPath.Contains("/Fan/");
            importer.animationType = importer.importAnimation ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
        }
        private void OnPostprocessModel(GameObject model)
        {
            if (!assetPath.Contains("/Fan/")) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips) { clip.loopTime = true; clip.loopPose = false; }
            importer.clipAnimations = clips;
        }
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/", StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.Contains("/Music/");
            bool effect = assetPath.Contains("/SFX/");
            importer.forceToMono = !music;
            importer.preloadAudioData = !music;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : effect ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = effect ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;
            settings.quality = .8f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Models/", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 1024;
            if (assetPath.ToLowerInvariant().Contains("normal")) importer.textureType = TextureImporterType.NormalMap;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
        }
    }
}
