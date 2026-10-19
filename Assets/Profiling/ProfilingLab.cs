using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRPractice.Profiling
{
    [DefaultExecutionOrder(-100)]
    public sealed class ProfilingLab : MonoBehaviour
    {
        public StressSpawner stress;
        public PhysicsStressTest physicsTest;
        public LightingBenchmarks lighting;
        public CameraCheckpoints checkpoints;
        public ProfilingCapture capture;
        public DesktopWorkshopController desktop;
        private LocomotionProvider[] locomotion;
        private bool[] previousLocomotion;
        private bool previousDesktop;
        private bool previousMenu;
        private bool initialized;
        private int countIndex, stage = 2, previousVsync, previousTargetRate;
        public string Description => "Stage " + stage + " | CP" + checkpoints.Current + " | N=" + stress.Count +
            " | " + stress.Series + " | " + stress.Scripts + " | Physics " + physicsTest.ColliderCase + "/" + physicsTest.Phase + " / bodies=" + physicsTest.BodyCount + " | Light " + lighting.Current;

        private void Awake()
        {
            initialized = true;
            previousVsync = QualitySettings.vSyncCount; previousTargetRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            locomotion = desktop.GetComponentsInChildren<LocomotionProvider>(true);
            previousLocomotion = new bool[locomotion.Length];
            capture.CaptureStateChanged += LockMovement;
        }
        private void Update()
        {
            if (capture.IsCapturing) return;
            if (desktop.menu && desktop.menu.IsOpen) return;
            var keys = Keyboard.current; if (keys == null) return;
            if (keys.backspaceKey.wasPressedThisFrame) ResetScene();
            if (keys.nKey.wasPressedThisFrame)
            {
                PrepareWorkload(); stage = 3; countIndex = (countIndex + 1) % StressSpawner.Counts.Length;
                stress.Spawn(StressSpawner.Counts[countIndex]);
            }
            if (keys.digit0Key.wasPressedThisFrame) { PrepareWorkload(); stage = 3; countIndex = 0; stress.Spawn(0); }
            if (keys.mKey.wasPressedThisFrame)
            {
                PrepareWorkload(); stage = 3;
                if (stress.Scripts != ScriptCase.None) stress.Spawn(stress.Count);
                stress.ConfigureSeries((GraphicsSeries)(((int)stress.Series + 1) % 4));
            }
            if (keys.bKey.wasPressedThisFrame || keys.gKey.wasPressedThisFrame)
            {
                PrepareWorkload(); stage = 4; stress.ConfigureSeries(GraphicsSeries.Baseline);
                stress.Spawn(500, keys.bKey.wasPressedThisFrame ? ScriptCase.Bad : ScriptCase.Good);
            }
            if (keys.vKey.wasPressedThisFrame) { PrepareWorkload(); stage = 3; stress.Spawn(stress.Count); }
            if (keys.pKey.wasPressedThisFrame || keys.oKey.wasPressedThisFrame)
            {
                stress.Clear(); stress.SetBenchmarkView(true); lighting.SetCase(LightingCase.Workshop); stage = 5;
                stress.RestoreWorkshopPipeline(); checkpoints.GoTo(2);
                physicsTest.Spawn(keys.pKey.wasPressedThisFrame);
                capture.Begin(Context(), 0); // Capture falling automatically, excluding allocation/cooking setup.
            }
            if (keys.lKey.wasPressedThisFrame)
            {
                stress.Clear(); stress.SetBenchmarkView(false); physicsTest.Clear(); stage = 5;
                stress.PrepareLightingPipeline(); checkpoints.GoTo(2);
                var next = lighting.Current == LightingCase.Baked ? LightingCase.Directional : lighting.Current + 1;
                lighting.SetCase(next);
            }
            if (keys.f9Key.wasPressedThisFrame)
            {
                if (!Application.isEditor && stage == 2) checkpoints.GoTo(3);
                capture.Begin(Context());
            }
        }

        private void PrepareWorkload() { physicsTest.Clear(); lighting.SetCase(LightingCase.Workshop); stress.RestoreWorkshopPipeline(); stress.SetBenchmarkView(true); checkpoints.GoTo(2); }
        public void ResetScene()
        {
            stress.Clear(); stress.ConfigureSeries(GraphicsSeries.Baseline); stress.SetBenchmarkView(false);
            stress.RestoreWorkshopPipeline();
            physicsTest.Clear(); lighting.SetCase(LightingCase.Workshop); stage = 2; countIndex = 0;
        }
        private CaptureContext Context()
        {
            string cp = "CP" + checkpoints.Current;
            var c = new CaptureContext { stage = stage.ToString(), checkpoint = cp, objectCount = physicsTest.BodyCount > 0 ? physicsTest.BodyCount : stress.Count,
                series = stage == 2 ? "Workshop" : stress.Series.ToString(), behaviour = stress.Scripts.ToString(),
                physicsCase = physicsTest.ColliderCase + "_" + physicsTest.Phase, lightingCase = lighting.Current.ToString() };
            if (stage == 2) c.label = Application.isEditor ? "Stage2_" + cp : "Stage6_Build";
            else if (stage == 3) c.label = "Stage3_" + c.series + "_N" + c.objectCount + "_" + cp;
            else if (stage == 4) c.label = "Stage4_" + c.behaviour + "_" + cp;
            else c.label = "Stage5_" + (physicsTest.ColliderCase == "None" ? "Lighting_" + c.lightingCase : "Physics_" + c.physicsCase) + "_" + cp;
            if (stage == 2 && !Application.isEditor) c.stage = "6";
            return c;
        }
        private void LockMovement(bool locked)
        {
            if (!desktop) return;
            if (locked)
            {
                previousDesktop = desktop.enabled; desktop.enabled = false;
                if (desktop.menu) { previousMenu = desktop.menu.enabled; desktop.menu.enabled = false; }
                for (int i = 0; i < locomotion.Length; i++) if (locomotion[i]) { previousLocomotion[i] = locomotion[i].enabled; locomotion[i].enabled = false; }
            }
            else
            {
                desktop.enabled = previousDesktop;
                if (desktop.menu) desktop.menu.enabled = previousMenu;
                for (int i = 0; i < locomotion.Length; i++) if (locomotion[i]) locomotion[i].enabled = previousLocomotion[i];
            }
        }
        private void OnDestroy()
        {
            if (!initialized) return;
            if (capture) { capture.Cancel(); capture.CaptureStateChanged -= LockMovement; }
            QualitySettings.vSyncCount = previousVsync; Application.targetFrameRate = previousTargetRate;
        }
    }
}
