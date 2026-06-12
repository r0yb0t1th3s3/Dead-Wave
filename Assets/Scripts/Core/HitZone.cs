using UnityEngine;

/// <summary>
/// Marks a collider as a special hit region (e.g. a zombie head).
/// WeaponController multiplies damage by this when the raycast lands here.
/// </summary>
public sealed class HitZone : MonoBehaviour
{
    public float damageMultiplier = 2f;
}
