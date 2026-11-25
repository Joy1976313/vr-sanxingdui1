using UnityEngine;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Desktop / phone-VR "side by side" stereo for the cave.
///
/// Two base cameras, separated by the interpupillary distance, render into the
/// left and right halves of the Game view. Their view frusta are shifted by
/// Camera.lensShift instead of toeing the cameras in, so the eyes stay parallel
/// and converge at `convergence` metres without introducing vertical parallax
/// (the classic off-axis stereo projection).
///
///   F7           : switch between stereo and normal single-camera view
///   stereoOn     : default state, also editable in the Inspector
///
/// If a real XR device is active (OpenXR / Quest etc.) the rig stands down and
/// leaves stereo to the XR system. This is a preview / Cardboard-style SBS
/// output, not an XR headset driver.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(200)]
public class CaveStereoRig : MonoBehaviour
{
    [Header("Side-by-side stereo")]
    public bool stereoOn = true;
    [Tooltip("Interpupillary distance in metres. 0.064 is the adult average.")]
    [Range(0.02f, 0.10f)] public float ipd = 0.064f;
    [Tooltip("Distance at which the two eyes converge (metres).")]
    [Min(0.5f)] public float convergence = 6f;
    public bool toggleWithF7 = true;
    [Tooltip("Thin black seam between the two eyes, useful in a phone VR viewer.")]
    public bool divider = true;

    [Header("Wiring (filled in by the editor setup script)")]
    public Camera headCamera;
    public Vector3 centerLocalPos = new Vector3(0f, 1.6f, 0f);
    public bool centerCaptured;

    Camera eyeR;
    bool built;
    float lastW, lastH;

    static readonly Rect Full = new Rect(0f, 0f, 1f, 1f);
    static readonly Rect LeftHalf = new Rect(0f, 0f, 0.5f, 1f);
    static readonly Rect RightHalf = new Rect(0.5f, 0f, 0.5f, 1f);

    void OnEnable() { Wake(); }
    void OnDisable() { Teardown(); }
    void OnDestroy() { Teardown(); }

    void FindHead()
    {
        if (headCamera != null) return;
        GameObject pc = GameObject.Find("Preview_Camera");
        if (pc != null) headCamera = pc.GetComponent<Camera>();
        if (headCamera == null) headCamera = Camera.main;
    }

    /// <summary>Remember the authored "centre eye" position exactly once.</summary>
    public void CaptureCenter()
    {
        if (headCamera == null) FindHead();
        if (headCamera != null && !centerCaptured)
        {
            centerLocalPos = headCamera.transform.localPosition;
            centerCaptured = true;
        }
    }

    public void Wake()
    {
        FindHead();
        CaptureCenter();
        if (headCamera == null) return;
        Rebuild();
    }

    static bool XrActive()
    {
        try
        {
            System.Type t = System.Type.GetType("UnityEngine.XR.XRSettings, UnityEngine.XRModule");
            if (t == null) return false;
            System.Reflection.PropertyInfo p = t.GetProperty("isDeviceActive");
            return p != null && (bool)p.GetValue(null, null);
        }
        catch { return false; }
    }

    void Rebuild()
    {
        Teardown();
        if (!stereoOn || headCamera == null || XrActive()) return;

        float e = ipd * 0.5f;

        // Left eye = the existing main camera. Its transform is still the one
        // the player controller pitches, we only shift its local X.
        headCamera.transform.localPosition = centerLocalPos + Vector3.left * e;
        headCamera.rect = LeftHalf;
        headCamera.clearFlags = CameraClearFlags.SolidColor;
        headCamera.depth = 0f;
        headCamera.lensShift = Vector2.zero;

        // Right eye = a sibling camera under the same parent (the Player), so
        // it inherits body yaw and gets the same pitch in LateUpdate.
        Transform parent = headCamera.transform.parent;
        GameObject go = new GameObject("Eye_R");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = centerLocalPos + Vector3.right * e;
        go.transform.localRotation = headCamera.transform.localRotation;

        Camera cam = go.AddComponent<Camera>();
        cam.CopyFrom(headCamera);
        cam.targetTexture = null;
        cam.rect = RightHalf;
        cam.depth = 1f;
        cam.clearFlags = CameraClearFlags.Depth;   // do not wipe the left half
        cam.lensShift = Vector2.zero;

        // URP keeps post-processing / volume settings on a separate component.
        UniversalAdditionalCameraData src = headCamera.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData dst = cam.GetUniversalAdditionalCameraData();
        dst.renderType = CameraRenderType.Base;
        dst.renderPostProcessing = src.renderPostProcessing;
        dst.renderShadows = src.renderShadows;
        dst.antialiasing = src.antialiasing;
        dst.antialiasingQuality = src.antialiasingQuality;
        dst.requiresColorOption = src.requiresColorOption;
        dst.requiresDepthOption = src.requiresDepthOption;
        dst.volumeLayerMask = src.volumeLayerMask;
        dst.cameraStack.Clear();

        eyeR = cam;
        built = true;
        lastW = Screen.width;
        lastH = Screen.height;
        ApplyShift();
    }

    void Teardown()
    {
        if (headCamera != null)
        {
            headCamera.targetTexture = null;
            headCamera.rect = Full;
            headCamera.lensShift = Vector2.zero;
            headCamera.clearFlags = CameraClearFlags.SolidColor;
            headCamera.depth = 0f;
            headCamera.transform.localPosition = centerLocalPos;

            Transform parent = headCamera.transform.parent;
            if (parent != null)
            {
                Transform old = parent.Find("Eye_R");
                if (old != null) Kill(old.gameObject);
            }
        }
        if (eyeR != null) Kill(eyeR.gameObject);
        eyeR = null;
        built = false;
    }

    static void Kill(GameObject go)
    {
        if (go == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) { Object.DestroyImmediate(go); return; }
#endif
        Object.Destroy(go);
    }

    /// <summary>
    /// Off-axis (parallel) stereo: shift each frustum sideways by
    /// (ipd/2 * near / convergence), expressed as Unity's lensShift units.
    /// Left eye shifts right, right eye shifts left, so a point at the
    /// convergence distance lands at the centre of both images.
    /// </summary>
    void ApplyShift()
    {
        if (!built || headCamera == null || eyeR == null) return;
        float near = Mathf.Max(0.001f, headCamera.nearClipPlane);
        float halfH = near * Mathf.Tan(headCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float halfW = halfH * Mathf.Max(0.1f, headCamera.aspect);
        float offset = (ipd * 0.5f) * near / Mathf.Max(0.5f, convergence);
        float s = offset / Mathf.Max(1e-5f, halfW);
        headCamera.lensShift = new Vector2(s, 0f);
        eyeR.lensShift = new Vector2(-s, 0f);
    }

    void LateUpdate()
    {
        HandleToggle();
        if (!built || !stereoOn) return;
        if (headCamera == null || eyeR == null) { Wake(); return; }

        eyeR.transform.localRotation = headCamera.transform.localRotation;
        Vector3 hp = headCamera.transform.localPosition;
        eyeR.transform.localPosition = new Vector3(centerLocalPos.x + ipd * 0.5f, hp.y, hp.z);

        if (Screen.width != lastW || Screen.height != lastH)
        {
            lastW = Screen.width;
            lastH = Screen.height;
            ApplyShift();
        }
    }

    void HandleToggle()
    {
        if (!toggleWithF7 || !Application.isPlaying) return;
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard kb = Keyboard.current;
        pressed = kb != null && kb.f7Key.wasPressedThisFrame;
#else
        pressed = Input.GetKeyDown(KeyCode.F7);
#endif
        if (pressed)
        {
            stereoOn = !stereoOn;
            Rebuild();
        }
    }

    void OnGUI()
    {
        if (!built || !divider || !stereoOn) return;
        Color old = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 1f, 0f, 2f, Screen.height),
                        Texture2D.whiteTexture);
        GUI.color = old;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) return;
        EditorApplication.delayCall -= EditorRebuild;
        EditorApplication.delayCall += EditorRebuild;
    }

    void EditorRebuild()
    {
        EditorApplication.delayCall -= EditorRebuild;
        if (this == null) return;
        Wake();
    }
#endif
}
