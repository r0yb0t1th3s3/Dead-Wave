using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Freeman's service rifle. Hitscan, hold-to-fire, magazine reload,
/// camera recoil via PlayerController.ExternalPitchOffset, muzzle flash,
/// and a small ring buffer of reusable impact puffs (no per-shot allocations).
/// Lives on the Player Camera object.
/// </summary>
public sealed class WeaponController : MonoBehaviour
{
    [Header("Ballistics")]
    [SerializeField] private float damage = 30f;
    [SerializeField] private float fireRate = 7.5f; // shots per second, hold to fire
    [SerializeField] private float range = 250f;

    [Header("Ammo")]
    [SerializeField] private int magazineSize = 30;
    [SerializeField] private int reserveAmmo = 150;
    [SerializeField] private float reloadSeconds = 2.2f;

    [Header("Feel")]
    [SerializeField] private float recoilKickDegrees = 1.1f;
    [SerializeField] private float recoilRecoverSpeed = 10f;
    [SerializeField] private float modelKickMeters = 0.05f;

    [Header("Wiring (set by stage builder)")]
    public Transform rifleModel;
    public Light muzzleLight;
    public Transform muzzleTip;

    /// <summary>(ammo in magazine, reserve)</summary>
    public event Action<int, int> OnAmmoChanged;
    public event Action<bool> OnReloadStateChanged;

    public int AmmoInMag => ammoInMag;
    public int Reserve => reserveAmmo;
    public bool IsReloading => reloading;

    private Camera viewCamera;
    private PlayerController player;
    private InputAction fireAction;
    private InputAction reloadAction;

    private int ammoInMag;
    private float nextShotTime;
    private bool reloading;
    private float reloadFinishTime;
    private float recoil;
    private float modelKick;
    private Vector3 rifleRestPosition;
    private float muzzleOffTime;

    private GameObject[] impactPuffs;
    private int puffIndex;

    private void Awake()
    {
        viewCamera = GetComponent<Camera>();
        player = GetComponentInParent<PlayerController>();

        fireAction = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
        reloadAction = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");

        ammoInMag = magazineSize;
        if (rifleModel != null)
        {
            rifleRestPosition = rifleModel.localPosition;
        }

        BuildImpactPuffs();
    }

    private void OnEnable()
    {
        fireAction.Enable();
        reloadAction.Enable();
    }

    private void OnDisable()
    {
        fireAction.Disable();
        reloadAction.Disable();
    }

    private void Update()
    {
        if (reloading && Time.time >= reloadFinishTime)
        {
            FinishReload();
        }

        if (reloadAction.WasPressedThisFrame())
        {
            TryStartReload();
        }

        bool cursorReady = Cursor.lockState == CursorLockMode.Locked
            && Time.time - PlayerController.LastCursorLockTime > 0.25f;

        if (cursorReady && !reloading && fireAction.IsPressed())
        {
            if (ammoInMag > 0 && Time.time >= nextShotTime)
            {
                Fire();
            }
            else if (ammoInMag == 0 && fireAction.WasPressedThisFrame())
            {
                TryStartReload(); // click on empty -> reload
            }
        }

        // Recoil recovery + viewmodel kick recovery + muzzle light timeout.
        recoil = Mathf.MoveTowards(recoil, 0f, recoilRecoverSpeed * Time.deltaTime);
        if (player != null)
        {
            player.ExternalPitchOffset = -recoil;
        }

        modelKick = Mathf.MoveTowards(modelKick, 0f, 0.4f * Time.deltaTime);
        if (rifleModel != null)
        {
            rifleModel.localPosition = rifleRestPosition + new Vector3(0f, 0f, -modelKick);
        }

        if (muzzleLight != null && Time.time > muzzleOffTime)
        {
            muzzleLight.intensity = 0f;
        }
    }

    private void Fire()
    {
        nextShotTime = Time.time + 1f / fireRate;
        ammoInMag--;
        OnAmmoChanged?.Invoke(ammoInMag, reserveAmmo);

        recoil = Mathf.Min(recoil + recoilKickDegrees, 6f);
        modelKick = modelKickMeters;

        if (muzzleLight != null)
        {
            muzzleLight.intensity = 2.4f;
            muzzleOffTime = Time.time + 0.05f;
        }

        Ray center = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        // Start the ray past the player's own capsule so we never shoot ourselves.
        Ray ray = new Ray(center.origin + center.direction * 0.5f, center.direction);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            float dealt = damage;

            HitZone zone = hit.collider.GetComponent<HitZone>();
            if (zone != null)
            {
                dealt *= zone.damageMultiplier;
            }

            Health target = hit.collider.GetComponentInParent<Health>();
            if (target != null)
            {
                target.TakeDamage(dealt);
            }

            ShowImpactPuff(hit.point + hit.normal * 0.02f);
        }
    }

    private void TryStartReload()
    {
        if (reloading || ammoInMag >= magazineSize || reserveAmmo <= 0)
        {
            return;
        }

        reloading = true;
        reloadFinishTime = Time.time + reloadSeconds;
        OnReloadStateChanged?.Invoke(true);
    }

    private void FinishReload()
    {
        int needed = magazineSize - ammoInMag;
        int taken = Mathf.Min(needed, reserveAmmo);
        ammoInMag += taken;
        reserveAmmo -= taken;
        reloading = false;

        OnAmmoChanged?.Invoke(ammoInMag, reserveAmmo);
        OnReloadStateChanged?.Invoke(false);
    }

    private void BuildImpactPuffs()
    {
        Material puffMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        puffMaterial.SetColor("_BaseColor", new Color(0.55f, 0.09f, 0.06f));

        impactPuffs = new GameObject[12];
        for (int i = 0; i < impactPuffs.Length; i++)
        {
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "Impact Puff";
            Destroy(puff.GetComponent<Collider>()); // must never eat raycasts
            puff.transform.localScale = Vector3.one * 0.09f;
            puff.GetComponent<Renderer>().sharedMaterial = puffMaterial;
            puff.AddComponent<AutoDisable>().lifetime = 0.25f;
            puff.SetActive(false);
            impactPuffs[i] = puff;
        }
    }

    private void ShowImpactPuff(Vector3 position)
    {
        GameObject puff = impactPuffs[puffIndex];
        puffIndex = (puffIndex + 1) % impactPuffs.Length;

        puff.SetActive(false); // restart AutoDisable timer
        puff.transform.position = position;
        puff.SetActive(true);
    }
}
