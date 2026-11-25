using UnityEngine;

/// <summary>
/// Warm, irregular torch-light flicker. Attach to a Point Light.
/// Modulates intensity and slightly shifts colour temperature so the light
/// feels like it is radiating from a real flame instead of a static bulb.
/// </summary>
[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    [Header("Flicker")]
    public float baseIntensity = 22f;
    public float baseRange = 9f;
    [Range(0f, 1f)] public float amount = 0.22f;
    public float speed = 7.5f;
    public float seed = 0f;

    Light lamp;
    Color baseColor;

    void Awake()
    {
        lamp = GetComponent<Light>();
        if (lamp != null)
        {
            baseIntensity = lamp.intensity;
            baseRange = lamp.range;
            baseColor = lamp.color;
        }
        if (Mathf.Approximately(seed, 0f)) seed = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (lamp == null) return;

        float t = Time.time * speed + seed;
        // Two noise bands: slow breathing + fast crackle.
        float slow = Mathf.PerlinNoise(t * 0.35f, seed);
        float fast = Mathf.PerlinNoise(seed, t * 1.7f);
        float n = slow * 0.65f + fast * 0.35f;      // 0..1
        float k = 1f + (n - 0.5f) * 2f * amount;    // 1 +/- amount

        lamp.intensity = baseIntensity * k;
        lamp.range = baseRange * (0.96f + 0.04f * k);
        // Keep the flicker in the same de-redded amber range as the rest of the scene.
        lamp.color = Color.Lerp(baseColor, new Color(1f, 0.85f, 0.62f), (k - 1f) * 0.8f + 0.12f);
    }
}
