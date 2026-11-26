using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 神树任务对话：玩家走近神树（进入触发区）自动播放字幕对话。
/// - 首次：讲述背景并授予"收回三件文物"主线 → GameProgress.TasksGiven = true
/// - 再次：显示简短提示（剩余数量）
/// 字幕为世界空间 UI，锚在神树冠下方，始终朝向玩家（公告板）。
/// 中文字体用系统微软雅黑（动态 OS 字体，无需字体资产）。
/// </summary>
[AddComponentMenu("三星堆/神树对话")]
public class TreeDialogue : MonoBehaviour
{
    [Header("首次对话内容")]
    [SerializeField] private string[] introLines =
    {
        "……三千年的沉眠，终于又有人推开了那扇青铜之门……",
        "旅人，我是青铜神树，古蜀的通天之木。",
        "三件圣物遗落在三重迷障之中——纵目面具、黄金权杖、还有神鸟。",
        "取回它们，唤醒我沉睡的枝叶，我将以神力护你归途。",
        "去吧，推开那三扇青铜之门。"
    };

    [Header("提示语（任务已授予后再靠近）")]
    [SerializeField] private string idleLine = "圣物尚在迷障之中……推开那扇门吧。";
    [SerializeField] private string idleDoneLine = "三件圣物已归位……古蜀之光，重照人间。";

    [Header("节奏")]
    [SerializeField] private float charSeconds = 0.13f;   // 每字停留
    [SerializeField] private float minDuration = 2.6f;    // 单句最短显示
    [SerializeField] private float fadeIn = 0.3f;

    [Header("字幕位置（相对本物体）")]
    [SerializeField] private Vector3 panelOffset = new Vector3(0f, 3.1f, 1.0f);
    [SerializeField] private float panelDistance = 900f;  // Canvas 像素宽

    private Canvas _canvas;
    private Text _text;
    private CanvasGroup _group;
    private Transform _cam;
    private Light _glow;
    private float _baseGlow;
    private Coroutine _playing;
    private float _idleCooldown;

    private void Awake()
    {
        BuildUI();
        var glowGo = GameObject.Find("TreeGlow");
        if (glowGo != null) { _glow = glowGo.GetComponent<Light>(); _baseGlow = _glow != null ? _glow.intensity : 0f; }
        _group.alpha = 0f;
    }

    /// <summary>运行时构建世界空间字幕面板（不污染场景文件，位置即到即用）</summary>
    private void BuildUI()
    {
        var go = new GameObject("TreeDialogueUI");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = panelOffset;

        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 50;

        var cg = go.AddComponent<CanvasGroup>();
        _group = cg;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(panelDistance, 170f);
        rt.localScale = Vector3.one * 0.0035f;   // ≈3.1m 宽

        // 背景
        var bg = new GameObject("BG", typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0.04f, 0.05f, 0.08f, 0.72f);

        // 文本（中文系统字体）
        var txtGo = new GameObject("Text", typeof(Text), typeof(Outline));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(26f, 18f); txtRt.offsetMax = new Vector2(-26f, -18f);
        _text = txtGo.GetComponent<Text>();
        _text.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei" }, 30);
        _text.fontSize = 34;
        _text.color = new Color(0.98f, 0.92f, 0.75f);   // 淡金
        _text.alignment = TextAnchor.MiddleCenter;
        _text.horizontalOverflow = HorizontalWrapMode.Wrap;
        _text.verticalOverflow = VerticalWrapMode.Overflow;
        _text.supportRichText = false;
        var outline = txtGo.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.6f, -1.6f);
        _text.text = "";
    }

    /// <summary>轮询测距触发（比 OnTriggerEnter 可靠：瞬移进入、物理时序边界都不漏）</summary>
    private bool _wasInside;

    private void Update()
    {
        if (_playing != null) return;
        var player = GameObject.Find("XR Origin (Complete)");
        if (player == null) return;
        var cc = player.GetComponent<CharacterController>();

        var sc = GetComponent<SphereCollider>();
        float radius = sc != null ? sc.radius : 4.6f;
        Vector3 center = transform.TransformPoint(sc != null ? sc.center : Vector3.up * 1.3f);
        Vector3 p = cc != null ? player.transform.TransformPoint(cc.center) : player.transform.position + Vector3.up;

        bool inside = Vector3.Distance(center, p) <= radius + 0.2f;
        if (!inside) { _wasInside = false; return; }
        if (_wasInside) return;   // 持续停留在区域内不重复触发
        _wasInside = true;

        if (!GameProgress.TasksGiven)
            _playing = StartCoroutine(PlayIntro());
        else if (GameProgress.ArtifactsReturned >= 3)
            _playing = StartCoroutine(PlaySingle(idleDoneLine));
        else if (Time.time > _idleCooldown)
            _playing = StartCoroutine(PlaySingle(idleLine + "（已回收 " + GameProgress.ArtifactsReturned + "/3）"));
    }

    private IEnumerator PlayIntro()
    {
        if (_glow != null) _glow.intensity = _baseGlow * 1.7f;   // 神树苏醒，辉光增强
        foreach (var line in introLines)
        {
            yield return ShowLine(line);
        }
        GameProgress.TasksGiven = true;
        if (_glow != null) _glow.intensity = _baseGlow * 1.25f;
        _playing = null;
    }

    private IEnumerator PlaySingle(string line)
    {
        _idleCooldown = Time.time + 12f;
        yield return ShowLine(line);
        _playing = null;
    }

    private IEnumerator ShowLine(string line)
    {
        _text.text = "【青铜神树】 " + line;
        // 淡入
        float t = 0f;
        while (t < fadeIn) { t += Time.deltaTime; _group.alpha = t / fadeIn; yield return null; }
        _group.alpha = 1f;
        // 停留（按字数计算）
        float dur = Mathf.Max(minDuration, line.Length * charSeconds);
        yield return new WaitForSeconds(dur);
        // 淡出
        t = 0f;
        while (t < fadeIn) { t += Time.deltaTime; _group.alpha = 1f - t / fadeIn; yield return null; }
        _group.alpha = 0f;
        _text.text = "";
    }

    private void LateUpdate()
    {
        // 公告板：字幕始终面向玩家相机
        if (_cam == null)
        {
            var c = Camera.main;
            if (c == null) return;
            _cam = c.transform;
        }
        var canvasT = transform.Find("TreeDialogueUI");
        if (canvasT == null) return;
        Vector3 toCam = _cam.position - canvasT.position;
        toCam.y = 0f;                       // 只绕 Y 轴转，避免字幕倾斜
        if (toCam.sqrMagnitude > 0.01f)
            canvasT.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);   // 世界空间UI的正面是-Z，需背向来向
    }
}
