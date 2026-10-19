using System;
using UnityEngine;

namespace VRPractice.Profiling
{
    public enum LightingCase { Workshop, Directional, RealtimeShadows, RealtimeNoShadows, Baked }

    public sealed class LightingBenchmarks : MonoBehaviour
    {
        public Light[] workshopPoints, shadowPoints, noShadowPoints, bakedPoints;
        public bool bakedLightingPrepared;
        public LightingCase Current { get; private set; }
        private bool[] originalEnabled;
        private LightmapData[] bakedMaps;
        private Renderer[] bakedRenderers;
        private int[] lightmapIndices;
        public string Status { get; private set; } = "";

        private void Awake()
        {
            originalEnabled = new bool[workshopPoints.Length];
            for (int i = 0; i < workshopPoints.Length; i++) originalEnabled[i] = workshopPoints[i].enabled;
            bakedMaps = LightmapSettings.lightmaps;
            var all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var renderers = new System.Collections.Generic.List<Renderer>();
            foreach (var renderer in all) if (renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534) renderers.Add(renderer);
            bakedRenderers = renderers.ToArray(); lightmapIndices = new int[bakedRenderers.Length];
            for (int i = 0; i < bakedRenderers.Length; i++) lightmapIndices[i] = bakedRenderers[i].lightmapIndex;
            SetCase(LightingCase.Workshop);
        }
        public bool SetCase(LightingCase next)
        {
            if (next == LightingCase.Baked && (!bakedLightingPrepared || bakedMaps == null || bakedMaps.Length == 0))
            { Status = "Baked недоступен: выполните Bake lighting в редакторе."; return false; }
            for (int i = 0; i < workshopPoints.Length; i++) workshopPoints[i].enabled = next == LightingCase.Workshop && originalEnabled[i];
            foreach (var light in shadowPoints) light.enabled = next == LightingCase.RealtimeShadows;
            foreach (var light in noShadowPoints) light.enabled = next == LightingCase.RealtimeNoShadows;
            foreach (var light in bakedPoints) light.enabled = false;
            LightmapSettings.lightmaps = next == LightingCase.Baked ? bakedMaps : Array.Empty<LightmapData>();
            for (int i = 0; i < bakedRenderers.Length; i++) if (bakedRenderers[i]) bakedRenderers[i].lightmapIndex = next == LightingCase.Baked ? lightmapIndices[i] : -1;
            Current = next; Status = ""; return true;
        }
        private void OnDestroy()
        {
            if (bakedMaps != null) LightmapSettings.lightmaps = bakedMaps;
            if (bakedRenderers != null) for (int i = 0; i < bakedRenderers.Length; i++) if (bakedRenderers[i]) bakedRenderers[i].lightmapIndex = lightmapIndices[i];
        }
    }
}
