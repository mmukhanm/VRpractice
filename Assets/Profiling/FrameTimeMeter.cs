using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace VRPractice.Profiling
{
    // Independent wall-clock frame time, NOT a replacement for Profiler CPU Main Thread.
    public sealed class FrameTimeMeter : MonoBehaviour
    {
        public Text panel;
        public Transform head;
        public ProfilingLab lab;
        public ProfilingCapture capture;
        private readonly float[] window = new float[120];
        private int cursor, filled;
        private float refresh;
        private bool captureLabelShown;
        public float MeanMs { get; private set; }
        public float MaxMs { get; private set; }

        private void LateUpdate()
        {
            window[cursor] = Time.unscaledDeltaTime * 1000;
            cursor = (cursor + 1) % window.Length; filled = Mathf.Min(filled + 1, window.Length);
            float sum = 0, maximum = 0;
            for (int i = 0; i < filled; i++) { sum += window[i]; maximum = Mathf.Max(maximum, window[i]); }
            MeanMs = sum / Mathf.Max(1, filled); MaxMs = maximum;
            panel.canvas.transform.SetPositionAndRotation(head.position + head.forward * 2, head.rotation);
            if (capture.IsCapturing)
            {
                if (!captureLabelShown) { panel.text = capture.Status; captureLabelShown = true; }
                return; // No string/Text regeneration in the measured steady state.
            }
            captureLabelShown = false;
            if (Time.unscaledTime < refresh) return;
            refresh = Time.unscaledTime + .5f;
            FrameSample f = capture.Latest;
            panel.text = "ПРОФИЛИРОВАНИЕ / АНДИЖАН МУХАММЕД\n" + lab.Description +
                "\nFRAME mean/max: " + Display(MeanMs) + " / " + Display(MaxMs) + " ms | FPS " + Display(1000 / Mathf.Max(.001f, MeanMs)) +
                "\nCPU Main " + Display(f.cpuMs) + " | Render " + Display(f.renderMs) + " | GPU " + Display(f.gpuMs) + " ms" +
                "\nBatches " + Display(f.batches) + " | SetPass " + Display(f.setPass) + " | Tris " + Display(f.tris) + " | Verts " + Display(f.verts) +
                "\nGC/frame " + Display(f.gcBytes) + " B | Memory " + Display(f.memoryBytes / 1048576) + " MiB" +
                "\n1/2/3 CP | N число | M оптимизация | B Bad | G Good | V без скрипта" +
                "\nP Convex | O Box | L освещение | F9 запись | Backspace базовая сцена" +
                "\n" + capture.Status + "\n" + lab.lighting.Status;
        }
        private static string Display(double value) => double.IsNaN(value) ? "N/A" : value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
