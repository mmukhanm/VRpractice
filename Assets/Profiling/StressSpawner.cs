using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VRPractice.Profiling
{
    public enum GraphicsSeries { Baseline, SRPBatcher, GPUInstancing, LOD }
    public enum ScriptCase { None, Bad, Good }

    public sealed class StressSpawner : MonoBehaviour
    {
        public static readonly int[] Counts = { 0, 100, 250, 500, 1000, 2000 };
        public GameObject prefab;
        public UniversalRenderPipelineAsset lightingPipelineTemplate;
        public UniversalRenderPipelineAsset workshopPipelineTemplate;
        public Vector3 zoneCenter = new Vector3(0, 0, 12);
        public Vector2 zoneSize = new Vector2(20, 20);
        public Renderer[] benchmarkOccluders;
        private readonly List<GameObject> objects = new List<GameObject>(2000);
        private UniversalRenderPipelineAsset pipeline, previousDefault, previousQuality;
        private UniversalRenderPipelineAsset lightingPipeline;
        private Material material;
        private bool[] originalVisibility;
        public int Count => objects.Count;
        public GraphicsSeries Series { get; private set; }
        public ScriptCase Scripts { get; private set; }

        private void Awake()
        {
            originalVisibility = new bool[benchmarkOccluders.Length];
            for (int i = 0; i < benchmarkOccluders.Length; i++) originalVisibility[i] = benchmarkOccluders[i] && benchmarkOccluders[i].enabled;
            previousDefault = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            previousQuality = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            var source = workshopPipelineTemplate ? workshopPipelineTemplate : previousQuality ? previousQuality : previousDefault;
            if (!source || !lightingPipelineTemplate) { Debug.LogError("Profiling requires both generated URP presets."); enabled = false; return; }
            pipeline = Instantiate(source); pipeline.name = "Profiling runtime pipeline";
            lightingPipeline = Instantiate(lightingPipelineTemplate); lightingPipeline.name = "Profiling lighting pipeline";
            QualitySettings.renderPipeline = pipeline;
            material = Instantiate(prefab.GetComponent<Renderer>().sharedMaterial);
            material.name = "Profiling runtime material";
            material.enableInstancing = false;
        }

        public void ConfigureSeries(GraphicsSeries series)
        {
            Series = series;
            if (pipeline) { QualitySettings.renderPipeline = pipeline; pipeline.useSRPBatcher = series == GraphicsSeries.SRPBatcher; }
            if (material) material.enableInstancing = series == GraphicsSeries.GPUInstancing;
            foreach (var obj in objects)
            {
                var lod = obj.GetComponent<LODGroup>();
                lod.enabled = series == GraphicsSeries.LOD;
                if (lod.enabled) lod.ForceLOD(-1);
                else
                {
                    var levels = lod.GetLODs();
                    for (int i = 0; i < levels.Length; i++)
                        foreach (var renderer in levels[i].renderers) renderer.enabled = i == 0;
                }
            }
        }

        public void Spawn(int count, ScriptCase scripts = ScriptCase.None)
        {
            if (count < 0 || count > 2000) throw new ArgumentOutOfRangeException(nameof(count));
            Clear(); Scripts = scripts;
            SetBenchmarkView(true);
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            for (int i = 0; i < count; i++)
            {
                // Identical deterministic positions in every optimization series.
                Vector3 position = PositionFor(i, columns, zoneCenter, zoneSize);
                var obj = Instantiate(prefab, position, Quaternion.identity, transform);
                obj.name = "Stress_" + i;
                foreach (var node in obj.GetComponentsInChildren<Transform>()) node.gameObject.isStatic = false;
                foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
                var lifetime = obj.AddComponent<StressObjectLifetime>(); lifetime.original = material;
                objects.Add(obj);
                if (scripts == ScriptCase.Bad) obj.AddComponent<BadBehaviour>();
                if (scripts == ScriptCase.Good) obj.AddComponent<GoodBehaviour>();
            }
            ConfigureSeries(Series);
        }

        public static Vector3 PositionFor(int index, int columns, Vector3 center, Vector2 size)
        {
            return center + new Vector3(((index % columns + .5f) / columns - .5f) * size.x,
                .025f, ((index / columns + .5f) / columns - .5f) * size.y);
        }

        public void SetBenchmarkView(bool enabledView)
        {
            // The same open view is used for N=0 and every Stage 3/4 series.
            for (int i = 0; i < benchmarkOccluders.Length; i++)
                if (benchmarkOccluders[i]) benchmarkOccluders[i].enabled = !enabledView && originalVisibility[i];
        }

        public void RestoreWorkshopPipeline()
        {
            var source = workshopPipelineTemplate ? workshopPipelineTemplate : previousQuality ? previousQuality : previousDefault;
            if (!pipeline || !source) return;
            QualitySettings.renderPipeline = pipeline;
            pipeline.useSRPBatcher = source.useSRPBatcher;
        }
        public void PrepareLightingPipeline()
        {
            if (lightingPipeline) QualitySettings.renderPipeline = lightingPipeline;
        }

        public void Clear()
        {
            foreach (var obj in objects) if (obj) { obj.SetActive(false); Destroy(obj); }
            objects.Clear(); Scripts = ScriptCase.None;
        }

        private void OnDestroy()
        {
            if (QualitySettings.renderPipeline == pipeline || QualitySettings.renderPipeline == lightingPipeline) QualitySettings.renderPipeline = previousQuality;
            if (material) Destroy(material);
            if (pipeline) Destroy(pipeline);
            if (lightingPipeline) Destroy(lightingPipeline);
        }
    }

}
