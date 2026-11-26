using UnityEngine;

/// <summary>
/// Wall torch that is dark until the player approaches, then fades up to full
/// brightness and fades back out when they leave.
///
/// Works together with TorchFlicker: instead of writing Light.intensity
/// directly (which would fight the flicker), we drive the flicker's
/// baseIntensity, so brightness and flicker are multiplied.
///
/// The flame meshes use a "cold" material variant in edit mode. At runtime the
/// glow is written through a MaterialPropertyBlock, so no material assets are
/// modified and all torches can still share the same two materials.
///
/// v22: every torch on the wall uses this component. There is no longer a
/// special always-lit pair floating in front of the camera - the first pair
/// simply sits close enough to the spawn that it is already lit on frame one,
/// then fades out as the player walks away.
/// </summary>
public class ProximityTorch : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("Legacy switch. Left false on all wall torches so the whole corridor uses approach-to-light.")]
    public bool alwaysOn = false;

    [Header("Proximity")]
    [Tooltip("Distance at which the torch starts to wake up.")]
    public float wakeDistance = 5.0f;
    [Tooltip("Distance at which it is fully lit.")]
    public float fullDistance = 1.8f;
    [Tooltip("How much the height difference counts. The corridor runs along Z, so sideways (X) offset is ignored entirely.")]
    [Range(0f, 1f)] public float verticalWeight = 0.35f;
    [Tooltip("Light intensity when fully lit.")]
    public float maxIntensity = 20f;
    [Tooltip("Glow units per second (fade in / out). Slower = more visible wake-up.")]
    public float fadeSpeed = 1.5f;

    [Header("Flame look (HDR)")]
    public Color litEmission = new Color(5.4f, 3.2f, 1.25f, 1f);
    public Color litTint = new Color(1.0f, 0.78f, 0.42f, 1f);
    [Tooltip("How dark the unlit flame head looks. Raised so a cold torch still has a readable silhouette.")]
    [Range(0f, 0.5f)] public float unlitTintScale = 0.22f;
    [Tooltip("Tiny residual ember glow so an unlit torch is still visible, but obviously NOT burning.")]
    [Range(0f, 0.2f)] public float emberFloor = 0.08f;

    Light lamp;
    TorchFlicker flicker;
    Transform player;
    Renderer[] flameParts;
    MaterialPropertyBlock block;
    float glow;

    void Awake()
    {
        lamp = GetComponentInChildren<Light>(true);
        flicker = GetComponentInChildren<TorchFlicker>(true);

        var all = GetComponentsInChildren<Renderer>(true);
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in all)
        {
            string n = r.name.ToLowerInvariant();
            if (n.Contains("flame") || n.Contains("ember")) list.Add(r);
        }
        flameParts = list.ToArray();
        block = new MaterialPropertyBlock();

        GameObject p = GameObject.Find("Player");
        if (p != null) player = p.transform;

        glow = alwaysOn ? 1f : 0f;
        Apply(glow);
    }

    void Update()
    {
        if (alwaysOn)
        {
            if (flicker != null) flicker.baseIntensity = maxIntensity;
            else if (lamp != null) lamp.intensity = maxIntensity;
            return;
        }
        if (player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) player = p.transform;
            else return;
        }

        // Torches are mounted on the side walls. Only the walk down the
        // corridor (Z) should bring them to life.
        Vector3 delta = player.position - transform.position;
        float d = Mathf.Abs(delta.z) + Mathf.Abs(delta.y) * verticalWeight;
        float target = 1f - Mathf.InverseLerp(fullDistance, wakeDistance, d);
        float next = Mathf.MoveTowards(glow, target, fadeSpeed * Time.deltaTime);

        if (!Mathf.Approximately(next, glow))
        {
            glow = next;
            Apply(glow);
        }
    }

    void Apply(float g)
    {
        if (flicker != null) flicker.baseIntensity = maxIntensity * g;
        else if (lamp != null) lamp.intensity = maxIntensity * g;

        if (flameParts == null || block == null) return;
        // A cold torch keeps a faint ember, so the player can spot the next
        // torch in the dark and see it actually catch fire on approach.
        Color emission = litEmission * Mathf.Max(g, emberFloor);
        Color tint = Color.Lerp(litTint * unlitTintScale, litTint, g);
        foreach (Renderer r in flameParts)
        {
            if (r == null) continue;
            block.SetColor("_EmissionColor", emission);
            block.SetColor("_BaseColor", tint);
            r.SetPropertyBlock(block);
        }
    }
}
