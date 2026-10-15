using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

// 3 s warm-up, then 60 s of recorded frame intervals on autopilot.
public class Bench
{
    const float Warmup = 3f;
    const float SlowMs = 33.3f;

    public float Duration = 60f;
    public bool QuitWhenDone;
    public bool Running { get; private set; }
    public string ResultsText { get; private set; } = "";

    float t;
    float attackClock;
    long lastTick;
    readonly List<float> samples = new List<float>(4000);
    readonly Stopwatch clock = Stopwatch.StartNew();

    public void Start()
    {
        Running = true;
        t = 0;
        attackClock = 0;
        lastTick = 0;
        samples.Clear();
    }

    // Spec target (6 cos 0.5t, 0, 6 sin 0.5t), Z flipped for Unity.
    public Vector3 Autopilot(float dt, Vector3 playerPos, out bool attack)
    {
        var target = new Vector3(6 * Mathf.Cos(0.5f * t), 0, -6 * Mathf.Sin(0.5f * t));
        var move = target - playerPos;
        move.y = 0;
        attackClock += dt;
        attack = false;
        if (attackClock >= 1f)
        {
            attackClock -= 1f;
            attack = true;
        }
        return Vector3.ClampMagnitude(move, 1f);
    }

    // Returns true once, when the run has finished. Uses the real clock, not Unity's delta.
    public bool Record(float dt)
    {
        long now = clock.ElapsedTicks;
        t += dt;
        if (t > Warmup && lastTick > 0)
            samples.Add((float)((now - lastTick) * 1000.0 / Stopwatch.Frequency));
        lastTick = now;
        if (t >= Warmup + Duration)
        {
            Running = false;
            ResultsText = Summarise();
            UnityEngine.Debug.Log(ResultsText);
            return true;
        }
        return false;
    }

    string Summarise()
    {
        var sorted = new List<float>(samples);
        sorted.Sort();
        int n = sorted.Count;
        double total = 0;
        int slow = 0;
        foreach (var ms in sorted)
        {
            total += ms;
            if (ms > SlowMs) slow++;
        }
        double avg = total / n;
        float p99 = sorted[Mathf.Min(n - 1, Mathf.CeilToInt(n * 0.99f) - 1)];
        return $"Unity {Application.unityVersion} ({SystemInfo.graphicsDeviceName})\n" +
               $"frames {n}\navg {avg:F2} ms ({1000 / avg:F1} fps)\np99 {p99:F2} ms\n" +
               $"worst {sorted[n - 1]:F2} ms\nover 33 ms: {slow}";
    }
}
