using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Profiling;
using UnityEngine;

namespace Poketato
{
    [Serializable]
    public class PerfSummary
    {
        public string scenario, quality, timeOfDay;
        public int frames, enemies, actors, projectiles, lights, transients, width, height;
        public float seconds, avgFps, avgMs, p50Ms, p95Ms, p99Ms, maxMs, simMs, simMsMax, cpuMs, cpuP95Ms;
        public int hitches33, hitches50, hitches100;
        public long drawCalls, batches, setPassCalls, triangles, heapMB;
    }

    /// <summary>
    /// Frame-time recorder used by the HUD overlay and the benchmark. Besides wall-clock frame time it measures
    /// main-thread CPU time (first Update of the frame until end of frame, i.e. scripts + render submission),
    /// which stays meaningful on machines where the GPU, not the CPU, sets the frame rate.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class PerfStats : MonoBehaviour
    {
        const int Capacity = 8192;
        readonly float[] frames = new float[Capacity];
        readonly float[] cpu = new float[Capacity];
        readonly Stopwatch frameWatch = new();
        int count, cpuCount;
        float elapsed;
        double simAcc;
        float simMax;
        int simCount;

        public float LastSimMs { get; private set; }
        public float LastCpuMs { get; private set; }
        public float SmoothedFps { get; private set; }

        ProfilerRecorder drawCalls, batches, setPass, triangles;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int PoketatoHeapSize();
#else
        static int PoketatoHeapSize() => (int)(GC.GetTotalMemory(false));
#endif

        void OnEnable()
        {
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        void OnDisable()
        {
            drawCalls.Dispose();
            batches.Dispose();
            setPass.Dispose();
            triangles.Dispose();
        }

        IEnumerator Start()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (!frameWatch.IsRunning) continue;
                float ms = (float)frameWatch.Elapsed.TotalMilliseconds;
                LastCpuMs = Mathf.Lerp(LastCpuMs, ms, 0.1f);
                if (cpuCount < Capacity) cpu[cpuCount++] = ms;
            }
        }

        public void ResetWindow()
        {
            count = 0;
            cpuCount = 0;
            elapsed = 0;
            simAcc = 0;
            simMax = 0;
            simCount = 0;
        }

        public void RecordSim(float ms)
        {
            LastSimMs = ms;
            simAcc += ms;
            simCount++;
            simMax = Mathf.Max(simMax, ms);
        }

        void Update()
        {
            frameWatch.Restart();
            float dt = Time.unscaledDeltaTime;
            if (count < Capacity) frames[count++] = dt * 1000f;
            elapsed += dt;
            SmoothedFps = Mathf.Lerp(SmoothedFps <= 0 ? 1f / Mathf.Max(dt, 1e-4f) : SmoothedFps, 1f / Mathf.Max(dt, 1e-4f), 0.05f);
        }

        public static long Value(ProfilerRecorder r) => r.Valid ? r.LastValue : -1;
        public long DrawCalls => Value(drawCalls);
        public long Batches => Value(batches);
        public long SetPass => Value(setPass);
        public long Triangles => Value(triangles);
        public static long HeapMB => PoketatoHeapSize() / (1024 * 1024);

        public PerfSummary Summarize(string scenario)
        {
            var s = new PerfSummary { scenario = scenario, frames = count, seconds = elapsed, width = Screen.width, height = Screen.height };
            if (count == 0) return s;
            var sorted = new float[count];
            Array.Copy(frames, sorted, count);
            Array.Sort(sorted);
            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                float f = sorted[i];
                sum += f;
                if (f > 33.4f) s.hitches33++;
                if (f > 50f) s.hitches50++;
                if (f > 100f) s.hitches100++;
            }
            float Pct(float p) => sorted[Mathf.Clamp(Mathf.CeilToInt(p * count) - 1, 0, count - 1)];
            s.avgMs = (float)(sum / count);
            s.avgFps = 1000f / s.avgMs;
            s.p50Ms = Pct(0.5f);
            s.p95Ms = Pct(0.95f);
            s.p99Ms = Pct(0.99f);
            s.maxMs = sorted[count - 1];
            s.simMs = simCount > 0 ? (float)(simAcc / simCount) : 0;
            if (cpuCount > 0)
            {
                var c = new float[cpuCount];
                Array.Copy(cpu, c, cpuCount);
                Array.Sort(c);
                double cs = 0;
                foreach (var v in c) cs += v;
                s.cpuMs = (float)(cs / cpuCount);
                s.cpuP95Ms = c[Mathf.Clamp(Mathf.CeilToInt(0.95f * cpuCount) - 1, 0, cpuCount - 1)];
            }
            s.simMsMax = simMax;
            s.drawCalls = DrawCalls;
            s.batches = Batches;
            s.setPassCalls = SetPass;
            s.triangles = Triangles;
            s.heapMB = HeapMB;
            return s;
        }
    }
}
