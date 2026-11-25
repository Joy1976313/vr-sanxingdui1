using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// First-person walker for the cave scene.
///   - Arrow keys / WASD : move (relative to facing)
///   - Mouse             : look left/right AND up/down
///   - Space             : jump
///   - Left Shift        : walk faster
///   - R                 : unstick (snap back to the last safe spot)
///   - Esc               : release the mouse
///   - Click in Game view: grab the mouse again
/// Requires a CharacterController (wall collision) and a child camera.
///
/// The cave shell contains thin, single-sided planes. If the player ever wedges
/// into one, the stuck detector puts them back at the last position that was
/// verified to be free of geometry.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class CavePlayerController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;

    [Header("Movement")]
    public float walkSpeed = 3.2f;
    public float sprintMultiplier = 1.8f;
    public float gravity = -18f;

    [Header("Jump")]
    [Tooltip("Peak height in metres for a single jump.")]
    public float jumpHeight = 1.15f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 0.12f;
    [Tooltip("Mouse now controls both yaw and pitch. Pitch starts from the camera's authored angle.")]
    public bool enableVerticalLook = true;
    public float minPitch = -55f;
    public float maxPitch = 65f;

    [Header("Anti-stuck")]
    [Tooltip("Seconds of blocked movement before snapping back to the last safe spot.")]
    public float stuckTimeout = 0.40f;
    [Tooltip("Press R at any time to unstick manually.")]
    public bool manualUnstickKey = true;

    CharacterController controller;
    float verticalVelocity;
    float pitch;
    Vector3 spawnPoint;
    Vector3 lastSafe;
    float killY;
    float stuckTimer;
    bool resetPressed;
    readonly Collider[] overlap = new Collider[24];

    // Set by the editor setup script; if it stays -999 no clamp is applied.
    [HideInInspector] public float groundLevel = -999f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        // Keep the authored 20-degree downward framing instead of snapping
        // the camera to level on the first frame of Play mode.
        if (cameraTransform != null)
        {
            float authored = cameraTransform.localEulerAngles.x;
            if (authored > 180f) authored -= 360f;
            pitch = Mathf.Clamp(authored, minPitch, maxPitch);
        }

        spawnPoint = transform.position;
        lastSafe = spawnPoint;
        killY = (groundLevel > -900f ? groundLevel : spawnPoint.y) - 15f;
        GrabMouse();
    }

    void GrabMouse()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        float mx = 0f, my = 0f, h = 0f, v = 0f;
        bool sprint = false, esc = false, click = false, jump = false;
        resetPressed = false;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb != null)
        {
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed) v += 1f;
            if (kb.downArrowKey.isPressed || kb.sKey.isPressed) v -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) h += 1f;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) h -= 1f;
            sprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            esc = kb.escapeKey.wasPressedThisFrame;
            jump = kb.spaceKey.wasPressedThisFrame;
            resetPressed = kb.rKey.wasPressedThisFrame;
        }
        if (mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue();
            mx = d.x;
            my = d.y;
            click = mouse.leftButton.wasPressedThisFrame;
        }
#else
        mx = Input.GetAxis("Mouse X") * 12f;
        my = Input.GetAxis("Mouse Y") * 12f;
        h = Input.GetAxis("Horizontal");
        v = Input.GetAxis("Vertical");
        sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        esc = Input.GetKeyDown(KeyCode.Escape);
        jump = Input.GetKeyDown(KeyCode.Space);
        resetPressed = Input.GetKeyDown(KeyCode.R);
        click = Input.GetMouseButtonDown(0);
#endif

        if (esc)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (click && Cursor.lockState != CursorLockMode.Locked)
        {
            GrabMouse();
        }

        // --- mouse look: yaw on the body, pitch on the camera ---
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            transform.Rotate(0f, mx * mouseSensitivity, 0f, Space.Self);

            if (enableVerticalLook && cameraTransform != null)
            {
                pitch = Mathf.Clamp(pitch - my * mouseSensitivity, minPitch, maxPitch);
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        // --- movement, relative to where we are facing ---
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = transform.right; right.y = 0f; right.Normalize();
        Vector3 dir = (fwd * v + right * h);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        float speed = walkSpeed * (sprint ? sprintMultiplier : 1f);

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;
            // v = sqrt(2 * g * h): exactly reaches jumpHeight, then gravity pulls back.
            if (jump) verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
        }
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 before = transform.position;
        Vector3 motion = (dir * speed + Vector3.up * verticalVelocity) * Time.deltaTime;
        controller.Move(motion);

        // --- anti-stuck ----------------------------------------------------
        // "Wedged inside a wall" looks like: the player is pressing a movement
        // key but the capsule hardly moves. After a short delay we return them
        // to the last position that was verified free of geometry.
        Vector3 after = transform.position;
        float travelled = Vector2.Distance(new Vector2(before.x, before.z),
                                          new Vector2(after.x, after.z));
        float intended = speed * Time.deltaTime;
        bool wantsMove = dir.sqrMagnitude > 0.01f;
        bool blocked = wantsMove && intended > 0.0001f && travelled < intended * 0.15f;

        if (blocked) stuckTimer += Time.deltaTime;
        else stuckTimer = 0f;

        bool embedded = InsideSolid();

        if (stuckTimer > stuckTimeout || (manualUnstickKey && resetPressed))
        {
            Unstick(false);
        }
        else if (!blocked && !embedded && controller.isGrounded)
        {
            lastSafe = transform.position;
        }

        // Safety net: never fall out of the level.
        if (transform.position.y < killY) Unstick(true);
    }

    /// <summary>
    /// Is a slightly-shrunk copy of the player capsule intersecting static
    /// geometry? Used only to decide whether the current spot is safe to
    /// remember, so a brief brush against a step cannot teleport the player.
    /// </summary>
    bool InsideSolid()
    {
        if (controller == null) return false;
        float r = controller.radius * 0.75f;
        float bottom = controller.center.y - controller.height * 0.5f;
        float top = controller.center.y + controller.height * 0.5f;
        Vector3 p0 = transform.position + Vector3.up * (bottom + r + 0.12f);
        Vector3 p1 = transform.position + Vector3.up * (top - r - 0.12f);

        int n = Physics.OverlapCapsuleNonAlloc(p0, p1, r, overlap,
                                               ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = overlap[i];
            if (c == null || c == controller || c is CharacterController) continue;
            if (c.transform.IsChildOf(transform)) continue;
            return true;
        }
        return false;
    }

    void Unstick(bool toSpawn)
    {
        Vector3 target = toSpawn ? spawnPoint : lastSafe;
        controller.enabled = false;
        transform.position = target + Vector3.up * 0.08f;
        controller.enabled = true;
        verticalVelocity = 0f;
        stuckTimer = 0f;
    }
}
