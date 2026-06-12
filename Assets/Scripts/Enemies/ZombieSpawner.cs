using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Spawns pooled walkers from the street-canyon spawn points.
/// Milestone 1 runs in continuous "combat test" trickle mode; Milestone 2
/// replaces the trickle with the day/night wave director.
/// Pooling is mandatory from day one - the endless-horde finale depends on it.
/// </summary>
public sealed class ZombieSpawner : MonoBehaviour
{
    [Header("Wiring (set by stage builder)")]
    public GameObject walkerPrefab;
    public Transform[] spawnPoints;
    public BarrierHealth barrier;

    [Header("Combat test settings (Milestone 1)")]
    [SerializeField] private float spawnInterval = 3.5f;
    [SerializeField] private int maxAlive = 12;

    private ObjectPool<ZombieController> pool;
    private readonly HashSet<ZombieController> alive = new HashSet<ZombieController>();
    private float nextSpawnTime;
    private bool halted;

    private void Awake()
    {
        pool = new ObjectPool<ZombieController>(
            CreateInstance,
            z => z.gameObject.SetActive(true),
            z => { z.gameObject.SetActive(false); alive.Remove(z); },
            z => { if (z != null) Destroy(z.gameObject); },
            collectionCheck: false,
            defaultCapacity: 32,
            maxSize: 128);

        if (barrier != null)
        {
            barrier.OnDestroyed += () => halted = true;
        }
    }

    private ZombieController CreateInstance()
    {
        GameObject go = Instantiate(walkerPrefab);
        ZombieController zombie = go.GetComponent<ZombieController>();
        zombie.Initialize(barrier, ReleaseZombie);
        return zombie;
    }

    private void ReleaseZombie(ZombieController zombie)
    {
        pool.Release(zombie);
    }

    private void Update()
    {
        if (halted || walkerPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            return;
        }

        if (alive.Count >= maxAlive || Time.time < nextSpawnTime)
        {
            return;
        }

        nextSpawnTime = Time.time + spawnInterval;

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Vector3 jitter = new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));

        ZombieController zombie = pool.Get();
        zombie.OnSpawned(point.position + jitter);
        alive.Add(zombie);
    }
}
