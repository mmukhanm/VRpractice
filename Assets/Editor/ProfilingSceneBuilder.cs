using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;
using VRPractice.Profiling;
using Object = UnityEngine.Object;

namespace VRPractice.Editor
{
    public static class ProfilingSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Profiling_Андижан.unity";
        private const string Folder = "Assets/Generated/Profiling";

        [MenuItem("VR Practice/Profiling/Create or rebuild scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(WorkshopBuilder.VariantScene)) { WorkshopBuilder.Build(); return; }
            BuildFromVariant();
            var scenes = EditorBuildSettings.scenes.Where(x => x.path != ScenePath).ToList();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void BuildFromVariant()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            if (File.Exists(ScenePath))
            {
                string backup = Path.Combine("Artifacts", "SceneBackups", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffff"));
                Directory.CreateDirectory(backup); File.Copy(ScenePath, Path.Combine(backup, Path.GetFileName(ScenePath)));
                if (File.Exists(ScenePath + ".meta")) File.Copy(ScenePath + ".meta", Path.Combine(backup, Path.GetFileName(ScenePath) + ".meta"));
                string lightingFolder = Path.Combine(Path.GetDirectoryName(ScenePath), Path.GetFileNameWithoutExtension(ScenePath));
                if (Directory.Exists(lightingFolder))
                    foreach (string file in Directory.GetFiles(lightingFolder, "*", SearchOption.AllDirectories))
                    {
                        string target = Path.Combine(backup, Path.GetRelativePath(Path.GetDirectoryName(ScenePath), file));
                        Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target);
                    }
            }
            var scene = EditorSceneManager.OpenScene(WorkshopBuilder.VariantScene);
            // Work on the actual scene copy; practice 6 keeps its original name and controls.
            EditorSceneManager.SaveScene(scene, ScenePath);
            var origin = Object.FindFirstObjectByType<XROrigin>(); origin.gameObject.name = "XR Origin (XR Rig)";
            var desktop = origin.GetComponent<DesktopWorkshopController>(); desktop.showHud = false;
            var rig = new GameObject("Profiling Laboratory");
            var capture = rig.AddComponent<ProfilingCapture>();
            var stress = rig.AddComponent<StressSpawner>();
            var physics = rig.AddComponent<PhysicsStressTest>();
            var lighting = rig.AddComponent<LightingBenchmarks>();
            var checkpoints = rig.AddComponent<CameraCheckpoints>();
            var lab = rig.AddComponent<ProfilingLab>();

            Mesh high = BuildWorkloadMesh();
            var material = GetMaterial();
            stress.prefab = BuildPrefab(high, material);
            stress.workshopPipelineTemplate = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            stress.lightingPipelineTemplate = LightingPipeline();
            stress.benchmarkOccluders = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(x => x.name.Contains("Room / back wall") || x.name.Contains("Room / ceiling")).Cast<Renderer>().ToArray();
            physics.mesh = high; physics.convexHull = ConvexHull(high.bounds); physics.material = material; capture.physicsTest = physics;
            checkpoints.origin = origin; checkpoints.capture = capture;
            checkpoints.points = new[]
            {
                Point("CP1", new Vector3(0, .05f, -3.5f), new Vector3(0, .05f, 1)),
                Point("CP2", new Vector3(0, .05f, 0), new Vector3(0, .05f, 12)),
                Point("CP3", new Vector3(-1.5f, .05f, 1.25f), new Vector3(-3.5f, .05f, 2.9f))
            };
            var originalPoints = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(x => x.type == LightType.Point).ToArray();
            lighting.workshopPoints = originalPoints;
            lighting.shadowPoints = Lights(rig.transform, "Realtime Soft", LightmapBakeType.Realtime, LightShadows.Soft);
            lighting.noShadowPoints = Lights(rig.transform, "Realtime No Shadows", LightmapBakeType.Realtime, LightShadows.None);
            lighting.bakedPoints = Lights(rig.transform, "Baked", LightmapBakeType.Baked, LightShadows.Soft);
            foreach (var light in lighting.shadowPoints.Concat(lighting.noShadowPoints).Concat(lighting.bakedPoints)) light.enabled = false;
            lab.stress = stress; lab.physicsTest = physics; lab.lighting = lighting; lab.checkpoints = checkpoints; lab.capture = capture; lab.desktop = desktop;
            var canvas = new GameObject("Frame time / World Space at 2 m", typeof(RectTransform));
            var ui = canvas.AddComponent<Canvas>(); ui.renderMode = RenderMode.WorldSpace; ui.worldCamera = origin.Camera;
            var rect = (RectTransform)canvas.transform; rect.sizeDelta = new Vector2(1100, 520); rect.localScale = Vector3.one * .00145f;
            canvas.transform.SetPositionAndRotation(origin.Camera.transform.position + origin.Camera.transform.forward * 2, origin.Camera.transform.rotation);
            var background = canvas.AddComponent<Image>(); background.color = new Color(.015f, .035f, .05f, .94f); background.raycastTarget = false;
            var textObj = new GameObject("Measurements", typeof(RectTransform)); textObj.transform.SetParent(canvas.transform, false);
            var tr = (RectTransform)textObj.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(24, 16); tr.offsetMax = new Vector2(-24, -16);
            var text = textObj.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 25;
            text.color = new Color(.8f, .94f, 1); text.raycastTarget = false; text.text = "Profiling_Андижан — нажмите Play";
            var meter = rig.AddComponent<FrameTimeMeter>(); meter.panel = text; meter.head = origin.Camera.transform;
            meter.lab = lab; meter.capture = capture;
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Profiling scene created. Existing scene, if any, backed up under Artifacts/SceneBackups. See Docs/profiling.md.");
        }

        private static Transform Point(string name, Vector3 position, Vector3 lookAt)
        {
            var obj = new GameObject(name); obj.transform.position = position;
            obj.transform.rotation = Quaternion.LookRotation((lookAt - position).normalized, Vector3.up); return obj.transform;
        }
        private static Material GetMaterial()
        {
            string path = Folder + "/StressMaterial.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = new Color(.23f, .5f, .65f);
                material.SetFloat("_Smoothness", .3f); AssetDatabase.CreateAsset(material, path);
            }
            // Retain instancing variants in player builds; the runtime clone explicitly
            // turns instancing OFF for every series except GPUInstancing.
            material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }

        private static UniversalRenderPipelineAsset LightingPipeline()
        {
            string path = Folder + "/LightingComparisonPipeline.asset";
            var preset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (!preset)
            {
                var source = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
                if (!source) throw new InvalidOperationException("A workshop URP asset is required.");
                preset = Object.Instantiate(source); AssetDatabase.CreateAsset(preset, path);
            }
            // These URP 17.6 setters are internal; configure a dedicated editor asset,
            // never use reflection or inaccessible runtime setters on the workshop asset.
            var serialized = new SerializedObject(preset);
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.FindProperty("m_AnyShadowsSupported").boolValue = true;
            serialized.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 8;
            serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            serialized.ApplyModifiedPropertiesWithoutUndo(); return preset;
        }

        private static Mesh BuildWorkloadMesh()
        {
            string path = Folder + "/ChairStress.asset";
            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (cached) return cached;
            var vertices = new List<Vector3>(); var indices = new List<int>();
            foreach (string sourcePath in new[] { "Assets/Models/Chair/Chair_Profiling.obj" })
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (!source) throw new InvalidOperationException("Missing profiling source: " + sourcePath);
                var root = Object.Instantiate(source);
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh mesh = filter.sharedMesh;
                    // Editor-only API can read import meshes without changing Read/Write in source .meta files.
                    using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
                    using (var positions = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp))
                    {
                        data[0].GetVertices(positions); int offset = vertices.Count;
                        var matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                        for (int i = 0; i < positions.Length; i++) vertices.Add(matrix.MultiplyPoint3x4(positions[i]));
                        for (int sub = 0; sub < data[0].subMeshCount; sub++)
                        using (var triangles = new NativeArray<int>(data[0].GetSubMesh(sub).indexCount, Allocator.Temp))
                        {
                            data[0].GetIndices(triangles, sub);
                            for (int i = 0; i < triangles.Length; i++) indices.Add(offset + triangles[i]);
                        }
                    }
                }
                Object.DestroyImmediate(root);
            }
            int triangleCount = indices.Count / 3;
            if (triangleCount < 2000 || triangleCount > 5000) throw new InvalidOperationException("Stress model must have 2000–5000 triangles: " + triangleCount);
            var bounds = new Bounds(vertices[0], Vector3.zero); foreach (var v in vertices) bounds.Encapsulate(v);
            float factor = .35f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            Vector3 center = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            for (int i = 0; i < vertices.Count; i++) vertices[i] = (vertices[i] - center) * factor;
            var result = new Mesh { name = "Practice 6 chair / " + triangleCount + " triangles" };
            result.SetVertices(vertices); result.SetTriangles(indices, 0); result.RecalculateBounds(); result.RecalculateNormals();
            AssetDatabase.CreateAsset(result, path); return result;
        }

        private static GameObject BuildPrefab(Mesh high, Material material)
        {
            string path = Folder + "/StressModule.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing) return existing;
            var root = new GameObject("Stress chair / practice 6");
            root.AddComponent<MeshFilter>().sharedMesh = high;
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            var low = GameObject.CreatePrimitive(PrimitiveType.Cube); low.name = "LOD1 proxy";
            Object.DestroyImmediate(low.GetComponent<Collider>()); low.transform.SetParent(root.transform, false);
            low.transform.localPosition = high.bounds.center; low.transform.localScale = high.bounds.size;
            var veryLow = GameObject.CreatePrimitive(PrimitiveType.Cube); veryLow.name = "LOD2 small proxy";
            Object.DestroyImmediate(veryLow.GetComponent<Collider>()); veryLow.transform.SetParent(root.transform, false);
            veryLow.transform.localPosition = high.bounds.center; veryLow.transform.localScale = high.bounds.size * .8f;
            var lod = root.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(.08f, new Renderer[] { renderer }), new LOD(.015f, new Renderer[] { low.GetComponent<Renderer>() }), new LOD(.002f, new Renderer[] { veryLow.GetComponent<Renderer>() }) });
            lod.RecalculateBounds();
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); return saved;
        }

        private static Mesh ConvexHull(Bounds bounds)
        {
            string path = Folder + "/PhysicsConvexHull.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing) return existing;
            const int sides = 12;
            var vertices = new Vector3[sides * 2]; var indices = new List<int>();
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides;
                    vertices[ring * sides + i] = bounds.center + Vector3.Scale(bounds.size,
                        new Vector3(Mathf.Cos(a) * .5f, ring - .5f, Mathf.Sin(a) * .5f));
                }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                indices.AddRange(new[] { i, i + sides, next, next, i + sides, next + sides });
            }
            for (int i = 1; i < sides - 1; i++) indices.AddRange(new[] { 0, i, i + 1, sides, sides + i + 1, sides + i });
            var mesh = new Mesh { name = "Convex proxy / 24 vertices / 44 triangles", vertices = vertices, triangles = indices.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static Light[] Lights(Transform parent, string name, LightmapBakeType mode, LightShadows shadows)
        {
            var lights = new Light[8];
            for (int i = 0; i < lights.Length; i++)
            {
                var obj = new GameObject(name + " " + (i + 1)); obj.transform.SetParent(parent, false);
                obj.transform.position = new Vector3((i % 4 - 1.5f) * 2.8f, 2.8f, i < 4 ? -2 : 2.5f);
                var light = obj.AddComponent<Light>(); light.type = LightType.Point; light.range = 8; light.intensity = 2;
                light.lightmapBakeType = mode; light.shadows = shadows; lights[i] = light;
            }
            return lights;
        }

        [MenuItem("VR Practice/Profiling/Bake lighting for comparison")]
        public static void BakeLighting()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open " + ScenePath);
            var lighting = Object.FindFirstObjectByType<LightingBenchmarks>();
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            bool[] states = lights.Select(l => l.enabled).ToArray();
            try
            {
                foreach (var light in lights) light.enabled = light.type == LightType.Directional || lighting.bakedPoints.Contains(light);
                foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    if (!renderer.GetComponentInParent<Rigidbody>() && !renderer.GetComponentInParent<Animator>())
                    {
                        var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                        GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags | StaticEditorFlags.ContributeGI);
                        if (renderer.name == "Ground")
                        {
                            var settings = new SerializedObject(renderer);
                            var scale = settings.FindProperty("m_ScaleInLightmap");
                            if (scale != null) { scale.floatValue = .1f; settings.ApplyModifiedPropertiesWithoutUndo(); }
                        }
                    }
                lighting.bakedLightingPrepared = Lightmapping.Bake() && LightmapSettings.lightmaps.Length > 0;
                if (!lighting.bakedLightingPrepared) throw new InvalidOperationException("Bake did not produce lightmaps. Baked mode remains unavailable.");
            }
            finally
            {
                for (int i = 0; i < lights.Length; i++) if (lights[i]) lights[i].enabled = states[i];
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
        }

        [MenuItem("VR Practice/Profiling/Build Windows Development + Autoconnect")]
        public static void BuildDevelopment()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Create the profiling scene first.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Install Windows Build Support for Unity 6000.6.0f1 through Unity Hub.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Builds/ProfilingWindows");
            var stress = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Folder + "/LightingComparisonPipeline.asset");
            if (!stress) throw new InvalidOperationException("Create the lighting preset first.");
            var previous = GraphicsSettings.defaultRenderPipeline;
            try
            {
                // Include shadow/soft-shadow variants needed by the runtime lighting preset.
                // The explicit workshop preset still defines the Stage 2/6 baseline.
                GraphicsSettings.defaultRenderPipeline = stress;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, target = BuildTarget.StandaloneWindows64,
                    locationPathName = "Builds/ProfilingWindows/VRpractice.exe",
                    options = BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging
                });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Development build failed: " + report.summary.result);
            }
            finally { GraphicsSettings.defaultRenderPipeline = previous; AssetDatabase.SaveAssets(); }
        }

        [MenuItem("VR Practice/Profiling/Validate scene")]
        public static void Validate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            var origin = GameObject.Find("XR Origin (XR Rig)");
            if (!origin || !origin.GetComponent<XROrigin>()) throw new InvalidOperationException("BadBehaviour requires the exact XR Origin name.");
            foreach (string name in new[] { "CP1", "CP2", "CP3" }) if (!GameObject.Find(name)) throw new InvalidOperationException("Missing " + name);
            var stress = Object.FindFirstObjectByType<StressSpawner>();
            int tris = stress.prefab.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
            if (tris < 2000 || tris > 5000 || !stress.prefab.GetComponent<Renderer>()) throw new InvalidOperationException("Invalid stress prefab.");
            if (stress.prefab.GetComponentsInChildren<Transform>().Any(t => t.gameObject.isStatic)) throw new InvalidOperationException("Stress prefab must not be Static.");
            if (!stress.lightingPipelineTemplate || !stress.lightingPipelineTemplate.supportsAdditionalLightShadows || stress.lightingPipelineTemplate.maxAdditionalLightsCount != 8)
                throw new InvalidOperationException("A separate lighting preset with eight lights and additional shadows is required.");
            var physics = Object.FindFirstObjectByType<PhysicsStressTest>();
            if (!physics.convexHull || physics.convexHull.vertexCount > 120) throw new InvalidOperationException("A bounded convex cooking proxy is required.");
            var lighting = Object.FindFirstObjectByType<LightingBenchmarks>();
            if (lighting.shadowPoints.Length != 8 || lighting.noShadowPoints.Length != 8 || lighting.bakedPoints.Length != 8) throw new InvalidOperationException("Eight point lights per case required.");
            if (Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length != 1) throw new InvalidOperationException("One AudioListener required.");
            Debug.Log("Profiling scene structural checks passed. Runtime measurements have not been performed by this command.");
        }
    }
}
