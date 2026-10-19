using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace VRPractice.Profiling
{
    [Serializable]
    public struct CaptureContext
    {
        public string stage, checkpoint, series, behaviour, physicsCase, lightingCase;
        public int objectCount;
        public string label;
    }

    public struct FrameSample
    {
        public double frameMs, cpuMs, renderMs, gpuMs, batches, setPass, tris, verts, gcBytes, memoryBytes, physicsMs;
        public int awakeBodies;
    }

    // Unsupported counters remain NaN in CSV, never a fabricated zero.
    internal sealed class Counter : IDisposable
    {
        private ProfilerRecorder recorder;
        private readonly double scale;
        private readonly bool positiveOnly;
        public Counter(ProfilerCategory category, string name, double multiplier = 1, bool requirePositive = false)
        {
            scale = multiplier; positiveOnly = requirePositive;
            try { recorder = ProfilerRecorder.StartNew(category, name, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame); }
            catch (Exception) { recorder = default; }
        }
        public double Value => recorder.Valid && recorder.Count > 0 && (!positiveOnly || recorder.LastValue > 0) ? recorder.LastValue * scale : double.NaN;
        public void Dispose() { if (recorder.Valid) recorder.Dispose(); }
    }

    [DefaultExecutionOrder(10000)]
    public sealed class ProfilingCapture : MonoBehaviour
    {
        private static readonly ProfilerMarker SampleMarker = new ProfilerMarker("ProfilingCapture.Sample");
        public int frameCount = 300;
        public PhysicsStressTest physicsTest;
        public event Action<bool> CaptureStateChanged;
        public bool IsCapturing { get; private set; }
        public string Status { get; private set; } = "F9 — запись 300 кадров";
        public string OutputDirectory { get; private set; }
        public FrameSample Latest { get; private set; }
        private Counter cpu, render, gpu, batches, setPass, tris, verts, gc, memory, physics;
        private FrameSample[] samples;
        private CaptureContext context;
        private int count, warmup;
        private string dataPath;
        private bool logging, previousProfilerEnabled;
        private bool pendingFinish;
        private string previousLogFile;

        private void OnEnable()
        {
            cpu = new Counter(ProfilerCategory.Internal, "Main Thread", 1e-6);
            render = new Counter(ProfilerCategory.Internal, "Render Thread", 1e-6);
            gpu = new Counter(ProfilerCategory.Render, "GPU Frame Time", 1e-6, true);
            batches = new Counter(ProfilerCategory.Render, "Batches Count");
            setPass = new Counter(ProfilerCategory.Render, "SetPass Calls Count");
            tris = new Counter(ProfilerCategory.Render, "Triangles Count");
            verts = new Counter(ProfilerCategory.Render, "Vertices Count");
            gc = new Counter(ProfilerCategory.Memory, "GC Allocated In Frame");
            memory = new Counter(ProfilerCategory.Memory, "Total Used Memory");
            physics = new Counter(ProfilerCategory.Physics, "Physics.Simulate", 1e-6);
        }

        private void Update()
        {
            Latest = new FrameSample
            {
                frameMs = Time.unscaledDeltaTime * 1000.0, cpuMs = cpu.Value, renderMs = render.Value,
                gpuMs = gpu.Value, batches = batches.Value, setPass = setPass.Value, tris = tris.Value,
                verts = verts.Value, gcBytes = gc.Value, memoryBytes = memory.Value, physicsMs = physics.Value,
                awakeBodies = physicsTest ? physicsTest.AwakeBodies : 0
            };
            if (!IsCapturing) return;
            if (pendingFinish) { Finish(true); return; }
            if (warmup > 0) { warmup--; return; }
            if (!logging)
            {
                // Begin after preparation/warmup; first sample comes next frame.
                previousProfilerEnabled = Profiler.enabled;
                previousLogFile = Profiler.logFile;
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    Profiler.enabled = false;
                    Profiler.logFile = dataPath;
                    Profiler.enableBinaryLog = true;
                    Profiler.enabled = true;
                }
                logging = true;
                warmup = 1; // Skip the partially recorded activation frame.
                return;
            }
            using (SampleMarker.Auto()) samples[count++] = Latest;
            if (count == samples.Length) pendingFinish = true; // Keep a full tail frame in the binary log.
        }

        public bool Begin(CaptureContext settings, int warmupFrames = 30)
        {
            if (IsCapturing) return false;
            if (Profiler.enableBinaryLog)
            { Status = "Завершите уже активную бинарную запись Profiler перед F9."; return false; }
            context = settings;
            string session = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            OutputDirectory = Path.Combine(Application.persistentDataPath, "Profiling", session);
            Directory.CreateDirectory(OutputDirectory);
            dataPath = Path.Combine(OutputDirectory, settings.label + ".data");
            samples = new FrameSample[Mathf.Max(300, frameCount)];
            count = 0; warmup = Mathf.Max(0, warmupFrames) + 1; logging = false; pendingFinish = false;
            File.WriteAllText(Path.Combine(OutputDirectory, "environment.txt"),
                "Unity: " + Application.unityVersion + "\nPlatform: " + Application.platform +
                "\nGPU: " + SystemInfo.graphicsDeviceName + "\nAPI: " + SystemInfo.graphicsDeviceType +
                "\nCPU: " + SystemInfo.processorType + "\nSystem RAM MiB: " + SystemInfo.systemMemorySize +
                "\nResolution: " + Screen.width + "x" + Screen.height + "\nFixed timestep: " + Time.fixedDeltaTime +
                "\nEditor: " + Application.isEditor + "\nDevelopment: " + Debug.isDebugBuild + "\n");
            Status = "ЗАПИСЬ: " + settings.label + " / " + samples.Length + " кадров. Команды и текст таблички приостановлены; держите голову и руки неподвижно.";
            IsCapturing = true;
            CaptureStateChanged?.Invoke(true);
            return true;
        }

        public void Cancel() { if (IsCapturing) Finish(false); }

        private void Finish(bool complete)
        {
            if (logging && (Application.isEditor || Debug.isDebugBuild))
            {
                Profiler.enabled = false; // Flush our binary log before file I/O and CSV formatting.
                Profiler.enableBinaryLog = false;
                Profiler.logFile = previousLogFile;
                Profiler.enabled = previousProfilerEnabled;
            }
            IsCapturing = false; logging = false;
            string csv = Path.Combine(OutputDirectory, context.label + ".csv");
            using (var writer = new StreamWriter(csv, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("label,stage,checkpoint,N,series,behaviour,physics_case,lighting_case,complete,frame,frame_ms,cpu_main_ms,render_thread_ms,gpu_ms,batches,setpass,tris,verts,gc_alloc_bytes,used_memory_bytes,physics_simulate_ms,awake_bodies");
                for (int i = 0; i < count; i++)
                {
                    FrameSample f = samples[i];
                    writer.WriteLine(string.Join(",", context.label, context.stage, context.checkpoint, context.objectCount,
                        context.series, context.behaviour, context.physicsCase, context.lightingCase, complete ? 1 : 0, i,
                        Number(f.frameMs), Number(f.cpuMs), Number(f.renderMs), Number(f.gpuMs), Number(f.batches),
                        Number(f.setPass), Number(f.tris), Number(f.verts), Number(f.gcBytes), Number(f.memoryBytes),
                        Number(f.physicsMs), f.awakeBodies));
                }
            }
            bool hasData = File.Exists(dataPath) && new FileInfo(dataPath).Length > 0;
            Status = (complete ? "Завершено: " : "НЕПОЛНАЯ запись: ") + count + " кадров. CSV: " + csv +
                (hasData ? " | .data сохранён" : " | .data не подтверждён; нужен Editor/Development Build");
            Debug.Log(Status);
            CaptureStateChanged?.Invoke(false);
        }

        private static string Number(double value) => double.IsNaN(value) ? "NaN" : value.ToString("0.######", CultureInfo.InvariantCulture);
        private void OnDisable()
        {
            Cancel();
            cpu?.Dispose(); render?.Dispose(); gpu?.Dispose(); batches?.Dispose(); setPass?.Dispose(); tris?.Dispose();
            verts?.Dispose(); gc?.Dispose(); memory?.Dispose(); physics?.Dispose();
        }
    }
}
