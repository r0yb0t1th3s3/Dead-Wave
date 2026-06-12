using System;
using UnityEngine;

/// <summary>
/// Generic damageable health. Used by zombies now; reusable for the player later.
/// Resets to max on enable so pooled objects come back alive.
/// </summary>
public sealed class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 60f;

    public float Max => maxHealth;
    public float Current { get; private set; }
    public bool IsDead => Current <= 0f;

    public event Action OnDeath;

    private void OnEnable()
    {
        Current = maxHealth;
    }

    public void ResetHealth()
    {
        Current = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead)
        {
            return;
        }

        Current -= amount;
        if (Current <= 0f)
        {
            Current = 0f;
            OnDeath?.Invoke();
        }
    }
}
