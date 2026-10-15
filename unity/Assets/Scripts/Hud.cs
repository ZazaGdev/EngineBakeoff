using System;
using UnityEngine;
using UnityEngine.UI;

// Joystick, attack button, benchmark button, FPS label and results panel.
// Built with uGUI; touches are read directly, so no EventSystem is needed.
public class Hud : MonoBehaviour
{
    const float StickRadius = 90f;
    const float RefWidth = 844f;
    const float RefHeight = 390f;

    public event Action AttackPressed;
    public event Action BenchPressed;

    RectTransform canvasRect;
    Text fpsLabel;
    Text resultsLabel;
    RectTransform stickBase;
    RectTransform stickKnob;
    RectTransform attackButton;
    RectTransform benchButton;
    int stickTouch = -1;
    Vector2 stickCenter;
    Vector2 stickVec;
    float fpsTimer;
    int fpsFrames;
    static Sprite circle;
    Font font;

    void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.matchWidthOrHeight = 1f;
        canvasRect = (RectTransform)transform;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        fpsLabel = MakeText(new Vector2(0, 1), new Vector2(16, -12), 18);
        resultsLabel = MakeText(new Vector2(0, 1), new Vector2(16, -40), 16);
        resultsLabel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);

        benchButton = MakeBox(new Vector2(1, 1), new Vector2(-16 - 67, -12 - 22), new Vector2(134, 44), new Color(0.2f, 0.2f, 0.2f, 0.8f), "Benchmark");
        attackButton = MakeBox(new Vector2(1, 0), new Vector2(-30 - 60, 30 + 60), new Vector2(120, 120), new Color(0.2f, 0.2f, 0.2f, 0.8f), "ATTACK");

        stickBase = MakeCircle(StickRadius, new Color(1, 1, 1, 0.15f));
        stickKnob = MakeCircle(36, new Color(1, 1, 1, 0.45f));
        PlaceStick(RestCenter());
    }

    Text MakeText(Vector2 anchor, Vector2 pos, int size)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(400, 200);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    RectTransform MakeBox(Vector2 anchor, Vector2 center, Vector2 size, Color color, string label)
    {
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        var text = MakeText(new Vector2(0.5f, 0.5f), Vector2.zero, 18);
        text.transform.SetParent(rt, false);
        text.rectTransform.sizeDelta = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = label;
        return rt;
    }

    RectTransform MakeCircle(float r, Color color)
    {
        var go = new GameObject("Circle", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.sizeDelta = new Vector2(r * 2, r * 2);
        var img = go.AddComponent<Image>();
        img.sprite = Circle();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    static Sprite Circle()
    {
        if (circle != null) return circle;
        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(s / 2f - d)));
            }
        tex.Apply();
        circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        return circle;
    }

    float Scale => canvasRect.rect.height > 0 ? Screen.height / canvasRect.rect.height : 1f;

    Vector2 RestCenter() => new Vector2(40 + StickRadius, 40 + StickRadius);

    void PlaceStick(Vector2 center)
    {
        stickCenter = center;
        stickBase.anchoredPosition = center;
        stickKnob.anchoredPosition = center + stickVec * StickRadius;
    }

    bool Hit(RectTransform rt, Vector2 screenPos) => RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);

    void Update()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            Vector2 pos = touch.position / Scale;
            if (touch.phase == TouchPhase.Began)
            {
                if (Hit(attackButton, touch.position)) AttackPressed?.Invoke();
                else if (Hit(benchButton, touch.position)) StartBench();
                else if (stickTouch == -1 && touch.position.x < Screen.width / 2f)
                {
                    stickTouch = touch.fingerId;
                    stickVec = Vector2.zero;
                    PlaceStick(pos);
                }
            }
            else if (touch.fingerId == stickTouch)
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    stickTouch = -1;
                    stickVec = Vector2.zero;
                    PlaceStick(RestCenter());
                }
                else
                {
                    stickVec = Vector2.ClampMagnitude((pos - stickCenter) / StickRadius, 1f);
                    PlaceStick(stickCenter);
                }
            }
        }
        if (Input.touchCount == 0 && Input.GetMouseButtonDown(0))
        {
            if (Hit(attackButton, Input.mousePosition)) AttackPressed?.Invoke();
            else if (Hit(benchButton, Input.mousePosition)) StartBench();
        }
        if (Input.GetKeyDown(KeyCode.Space)) AttackPressed?.Invoke();
        if (Input.GetKeyDown(KeyCode.B)) StartBench();
    }

    void StartBench()
    {
        resultsLabel.text = "running...";
        BenchPressed?.Invoke();
    }

    // Joystick or keyboard direction on the ground plane (x right, z away from the camera).
    public Vector3 MoveVector()
    {
        var v = stickVec;
        if (v == Vector2.zero)
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v.x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v.x += 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v.y += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v.y -= 1;
        }
        return Vector3.ClampMagnitude(new Vector3(v.x, 0, v.y), 1f);
    }

    // Shows frames counted over the last half second, like Godot's built-in counter.
    public void SetFps(float dt)
    {
        fpsFrames++;
        fpsTimer += dt;
        if (fpsTimer >= 0.5f)
        {
            fpsLabel.text = $"{Mathf.RoundToInt(fpsFrames / fpsTimer)} fps";
            fpsFrames = 0;
            fpsTimer = 0;
        }
    }

    public void ShowResults(string text) => resultsLabel.text = text;
}
