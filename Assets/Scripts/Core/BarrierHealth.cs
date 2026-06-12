using System;
using UnityEngine;

/// <summary>
/// The Hesco Bastion's health pool. Milestone 1: a single 100% pool.
/// Milestone 2 expands this into Tiers I-V (100%..500%) with tier-loss rules.
/// The gate shares this pool by design - there is no separate gate health.
/// </summary>
public sealed class BarrierHealth : MonoBehaviour
{
    [SerializeField] private float maxPercent = 100f;

    public float Current { get; private set; }
    public float Max => maxPercent;
    public bool IsDestroyed => Current <= 0f;

    /// <summary>(current, max)</summary>
    public event Action<float, float> OnChanged;
    public event Action OnDestroyed;

    private void Awake()
    {
        Current = maxPercent;
    }

    public void TakeDamage(float percent)
    {
        if (IsDestroyed)
        {
            return;
        }

        Current = Mathf.Max(0f, Current - percent);
        OnChanged?.Invoke(Current, maxPercent);

        if (Current <= 0f)
        {
            OnDestroyed?.Invoke();
        }
    }

    public void Repair(float percent)
    {
        if (IsDestroyed)
        {
            return; // a destroyed barrier is game over; no posthumous repairs
        }

        Current = Mathf.Min(maxPercent, Current + percent);
        OnChanged?.Invoke(Current, maxPercent);
    }
}
