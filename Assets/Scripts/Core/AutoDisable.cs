using UnityEngine;

/// <summary>
/// Deactivates the GameObject a fixed time after it is enabled.
/// Used for pooled one-shot effects like bullet impact puffs.
/// </summary>
public sealed class AutoDisable : MonoBehaviour
{
    public float lifetime = 0.25f;

    private float enabledAt;

    private void OnEnable()
    {
        enabledAt = Time.time;
    }

    private void Update()
    {
        if (Time.time - enabledAt >= lifetime)
        {
            gameObject.SetActive(false);
        }
    }
}
