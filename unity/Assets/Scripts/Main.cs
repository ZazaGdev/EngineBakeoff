using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the test scene from docs/scene-spec.md and runs the game loop.
// Unity is left-handed with +Z forward; the spec's +Z (towards the camera) maps to -Z here.
public class Main : MonoBehaviour
{
    const float RoomHalf = 11f;
    const float PlayerSpeed = 4f;
    const float AttackCooldown = 0.8f;
    const float AttackRange = 2.2f;
    const int EnemyCount = 20;
    const float EnemySpeed = 2.2f;
    const float EnemyStop = 1.5f;
    const float EnemyAttackEvery = 1.6f;
    const float Separation = 1.4f;
    static readonly Vector3 CameraOffset = new Vector3(0, 10, -8);
    static readonly string[] KnightHidden = { "1H_Sword_Offhand", "Rectangle_Shield", "Round_Shield", "Spike_Shield", "2H_Sword" };

    class Enemy { public Transform t; public Animator anim; public Vector3 spawn; public float cooldown; public string state; }

    Transform player;
    Animator playerAnim;
    string playerState = "";
    Vector3 playerFacing = new Vector3(0, 0, -1);
    float attackTimer;
    readonly List<Enemy> enemies = new List<Enemy>();
    Camera cam;
    Hud hud;
    Bench bench;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<Main>() == null) new GameObject("Main").AddComponent<Main>();
    }

    void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        BuildEnvironment();
        BuildRoom();
        BuildPlayer();
        BuildEnemies();
        var camGo = new GameObject("Camera");
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
        cam.allowMSAA = false;
        cam.allowHDR = false;
        hud = new GameObject("Hud").AddComponent<Hud>();
        hud.AttackPressed += TryAttack;
        hud.BenchPressed += StartBench;
        bench = new Bench();
        UpdateCamera();
        // Desktop check: `-autobench=SECONDS` runs a short benchmark and quits.
        foreach (var arg in System.Environment.GetCommandLineArgs())
        {
            if (arg.StartsWith("-autobench="))
            {
                bench.Duration = float.Parse(arg.Substring(11), System.Globalization.CultureInfo.InvariantCulture);
                bench.QuitWhenDone = true;
                StartBench();
            }
            if (arg.StartsWith("-shot=")) shotPath = arg.Substring(6);
        }
    }

    string shotPath;

    void BuildEnvironment()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.42f);
        RenderSettings.fog = false;
        RenderSettings.skybox = null;
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        // Spec pitch -50 and yaw 30 give the direction (-0.321, -0.766, -0.557) in Godot's
        // axes; Z flipped for Unity.
        sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.321f, -0.766f, 0.557f));
        sun.intensity = 1f;
        sun.color = Color.white;
        sun.shadows = LightShadows.Hard;
    }

    void BuildRoom()
    {
        var floor = Resources.Load<GameObject>("Models/floor_tile_large");
        var wall = Resources.Load<GameObject>("Models/wall");
        var pillar = Resources.Load<GameObject>("Models/pillar");
        for (int ix = 0; ix < 6; ix++)
            for (int iz = 0; iz < 6; iz++)
                Instantiate(floor, new Vector3(-10 + ix * 4, 0, -10 + iz * 4), Quaternion.identity);
        for (int i = 0; i < 6; i++)
        {
            float c = -10 + i * 4;
            Instantiate(wall, new Vector3(c, 0, 12), Quaternion.Euler(0, 180, 0));
            Instantiate(wall, new Vector3(c, 0, -12), Quaternion.identity);
            Instantiate(wall, new Vector3(-12, 0, c), Quaternion.Euler(0, 90, 0));
            Instantiate(wall, new Vector3(12, 0, c), Quaternion.Euler(0, -90, 0));
        }
        foreach (var p in new[] { new Vector3(-6, 0, -6), new Vector3(6, 0, -6), new Vector3(-6, 0, 6), new Vector3(6, 0, 6) })
            Instantiate(pillar, p, Quaternion.identity);
    }

    void BuildPlayer()
    {
        player = Instantiate(Resources.Load<GameObject>("Models/Knight")).transform;
        foreach (var r in player.GetComponentsInChildren<Renderer>(true))
            if (System.Array.IndexOf(KnightHidden, r.gameObject.name) >= 0) r.gameObject.SetActive(false);
        playerAnim = player.GetComponentInChildren<Animator>();
        playerAnim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Knight");
        Play(playerAnim, ref playerState, "Idle", 0);
    }

    void BuildEnemies()
    {
        var prefab = Resources.Load<GameObject>("Models/Skeleton_Minion");
        var ctrl = Resources.Load<RuntimeAnimatorController>("Skeleton");
        for (int i = 0; i < EnemyCount; i++)
        {
            float a = i * 18f * Mathf.Deg2Rad;
            var e = new Enemy { spawn = new Vector3(10 * Mathf.Cos(a), 0, -10 * Mathf.Sin(a)), state = "" };
            e.t = Instantiate(prefab).transform;
            e.anim = e.t.GetComponentInChildren<Animator>();
            e.anim.runtimeAnimatorController = ctrl;
            enemies.Add(e);
        }
        ResetPositions();
    }

    static void Play(Animator anim, ref string current, string state, float fade)
    {
        current = state;
        anim.CrossFadeInFixedTime(state, fade, 0, 0f);
    }

    void ResetPositions()
    {
        player.position = Vector3.zero;
        foreach (var e in enemies) { e.t.position = e.spawn; e.cooldown = 0; }
    }

    void StartBench()
    {
        ResetPositions();
        bench.Start();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Vector3 move;
        bool autoAttack = false;
        if (bench.Running)
        {
            move = bench.Autopilot(dt, player.position, out autoAttack);
        }
        else
        {
            move = hud.MoveVector();
        }

        if (move.magnitude > 0.05f)
        {
            playerFacing = move.normalized;
            var p = player.position + Vector3.ClampMagnitude(move, 1f) * PlayerSpeed * dt;
            p.x = Mathf.Clamp(p.x, -RoomHalf, RoomHalf);
            p.z = Mathf.Clamp(p.z, -RoomHalf, RoomHalf);
            player.position = p;
            player.rotation = Quaternion.LookRotation(playerFacing);
            if (attackTimer <= 0 && playerState != "Running_A") Play(playerAnim, ref playerState, "Running_A", 0.15f);
        }
        else if (attackTimer <= 0 && playerState != "Idle")
        {
            Play(playerAnim, ref playerState, "Idle", 0.15f);
        }

        attackTimer -= dt;
        if (autoAttack) TryAttack();

        UpdateEnemies(dt);
        UpdateCamera();
        hud.SetFps(dt);
        if (bench.Running && bench.Record(dt))
        {
            hud.ShowResults(bench.ResultsText);
            if (shotPath != null) ScreenCapture.CaptureScreenshot(shotPath);
            if (bench.QuitWhenDone) Invoke(nameof(Quit), 0.5f);
        }
    }

    void Quit() => Application.Quit();

    void TryAttack()
    {
        if (attackTimer > 0) return;
        attackTimer = AttackCooldown;
        Play(playerAnim, ref playerState, "1H_Melee_Attack_Chop", 0.05f);
        foreach (var e in enemies)
        {
            var to = e.t.position - player.position;
            to.y = 0;
            if (to.magnitude < AttackRange && Vector3.Dot(to.normalized, playerFacing) > 0.3f)
            {
                e.t.position = e.spawn;
                e.cooldown = 0;
            }
        }
    }

    void UpdateEnemies(float dt)
    {
        foreach (var e in enemies)
        {
            var to = player.position - e.t.position;
            to.y = 0;
            float dist = to.magnitude;
            if (dist > 0.01f) e.t.rotation = Quaternion.LookRotation(to);
            e.cooldown -= dt;
            if (dist > EnemyStop)
            {
                e.t.position += to / dist * EnemySpeed * dt;
                if (e.state != "Running_A") Play(e.anim, ref e.state, "Running_A", 0.15f);
            }
            else if (e.cooldown <= 0)
            {
                e.cooldown = EnemyAttackEvery;
                Play(e.anim, ref e.state, "1H_Melee_Attack_Chop", 0.1f);
            }
            else if (e.state == "1H_Melee_Attack_Chop" && e.anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
            {
                Play(e.anim, ref e.state, "Idle", 0.1f);
            }
        }
        for (int i = 0; i < enemies.Count; i++)
        {
            for (int j = i + 1; j < enemies.Count; j++)
            {
                var a = enemies[i].t;
                var b = enemies[j].t;
                var d = b.position - a.position;
                d.y = 0;
                float l = d.magnitude;
                if (l < Separation && l > 0.0001f)
                {
                    var push = d / l * (Separation - l) * 0.5f;
                    a.position -= push;
                    b.position += push;
                }
            }
        }
    }

    void UpdateCamera()
    {
        cam.transform.position = player.position + CameraOffset;
        cam.transform.LookAt(player.position, Vector3.up);
    }
}
