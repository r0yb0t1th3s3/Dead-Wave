using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the Dead Wave main stage as a saved, authored scene.
/// Menu: Dead Wave > Build Stage Scene
///
/// v2 — Lower Manhattan layout (fictional Hudson bridge at the Holland Tunnel site):
///   +Z = north-east, into the city (zombie side)
///   -Z = south-west, down the bridge over the Hudson (player HQ / escape route)
///   4-lane waterfront road, three street canyons as spawn routes, layered skyline,
///   Hesco line with a GATE at the east end for squad runs (gate shares barrier health).
///
/// Re-running the menu item rebuilds and overwrites the scene. Materials are real
/// assets in Assets/Materials so the scene survives saving and version control.
/// </summary>
public static class DeadWaveStageBuilder
{
    private const string ScenePath = "Assets/Scenes/DeadWave_Stage.unity";

    // AKM viewmodel mounting. The rifle is auto-measured and auto-fitted at build
    // time, so import scale and pivot don't matter. If it points backwards, set
    // AkmFlip180 = true and rebuild.
    private const bool AkmFlip180 = true;
    private const string AkmPrefabPath = "Assets/DelthorGames/AKM/Prefabs/AKM.prefab";
    private const float RifleTargetLength = 0.95f; // meters, stock to muzzle
    private static readonly Vector3 RifleMountLocal = new Vector3(0.24f, -0.2f, 0.5f); // camera space
    private static readonly Vector3 MuzzleTipFallbackLocal = new Vector3(0.24f, -0.16f, 1.0f);

    [MenuItem("Dead Wave/Build Stage Scene")]
    public static void BuildStageScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Scenes");

        // --- Materials -----------------------------------------------------
        Material asphalt = GetOrCreateMaterial("Asphalt", new Color(0.16f, 0.17f, 0.18f));
        Material concrete = GetOrCreateMaterial("Concrete", new Color(0.55f, 0.55f, 0.52f));
        Material sidewalk = GetOrCreateMaterial("Sidewalk", new Color(0.62f, 0.61f, 0.58f));
        Material cityGround = GetOrCreateMaterial("City_Ground", new Color(0.30f, 0.30f, 0.29f));
        Material brick = GetOrCreateMaterial("Building_Brick", new Color(0.45f, 0.28f, 0.22f));
        Material greyBlock = GetOrCreateMaterial("Building_Grey", new Color(0.50f, 0.52f, 0.55f));
        Material tanBlock = GetOrCreateMaterial("Building_Tan", new Color(0.60f, 0.55f, 0.45f));
        Material darkTower = GetOrCreateMaterial("Building_DarkTower", new Color(0.32f, 0.36f, 0.42f));
        Material hescoSand = GetOrCreateMaterial("Hesco_Sand", new Color(0.62f, 0.53f, 0.38f));
        Material hescoFace = GetOrCreateMaterial("Hesco_Face", new Color(0.45f, 0.39f, 0.28f));
        Material steel = GetOrCreateMaterial("Gate_Steel", new Color(0.20f, 0.22f, 0.24f), 0.45f);
        Material water = GetOrCreateMaterial("River_Water", new Color(0.05f, 0.18f, 0.22f), 0.85f);
        Material stripe = GetOrCreateMaterial("Road_Stripe", new Color(0.85f, 0.77f, 0.45f));
        Material hqDeck = GetOrCreateMaterial("HQ_Deck", new Color(0.13f, 0.14f, 0.15f));
        Material glow = GetOrCreateMaterial("Warning_Glow", new Color(1f, 0.45f, 0.12f), 0.2f, true);
        Material carRed = GetOrCreateMaterial("Car_Red", new Color(0.50f, 0.12f, 0.10f));
        Material carBlue = GetOrCreateMaterial("Car_Blue", new Color(0.12f, 0.20f, 0.45f));
        Material carWhite = GetOrCreateMaterial("Car_White", new Color(0.78f, 0.78f, 0.78f));
        Material carTaxi = GetOrCreateMaterial("Car_Taxi", new Color(0.85f, 0.65f, 0.10f));
        Material glass = GetOrCreateMaterial("Car_Glass", new Color(0.08f, 0.10f, 0.12f), 0.7f);
        Material skybox = GetOrCreateSkybox();

        // --- Fresh scene ---------------------------------------------------
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Transform world = new GameObject("World").transform;
        Transform city = NewGroup("City", world);
        Transform road = NewGroup("Road", world);
        Transform bridge = NewGroup("Bridge", world);
        Transform barrier = NewGroup("Barrier", world);
        Transform gate = NewGroup("Gate", barrier);

        // --- Lighting & sky (day one of the apocalypse still has color) ------
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.60f);

        GameObject sun = new GameObject("Sun");
        Light sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.intensity = 1.25f;
        sunLight.color = new Color(1f, 0.93f, 0.82f);
        sunLight.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

        // --- Water (the Hudson) ----------------------------------------------
        Box(bridge, water, "Hudson", new Vector3(0f, -3f, -20f), new Vector3(320f, 0.1f, 320f));

        // Seawall along the Manhattan shore, split so it doesn't fight the deck
        Box(road, concrete, "Seawall West", new Vector3(-63f, -1.55f, 9.75f), new Vector3(114f, 3f, 0.5f));
        Box(road, concrete, "Seawall East", new Vector3(63f, -1.55f, 9.75f), new Vector3(114f, 3f, 0.5f));

        // --- 4-lane waterfront road (z 10..25) --------------------------------
        Box(road, asphalt, "Road Deck", new Vector3(0f, -0.17f, 17.5f), new Vector3(240f, 0.34f, 15f));
        Box(road, sidewalk, "Sidewalk", new Vector3(0f, -0.11f, 27f), new Vector3(240f, 0.34f, 4f));
        Box(city, cityGround, "City Ground", new Vector3(0f, -0.17f, 72.5f), new Vector3(240f, 0.34f, 95f));

        // Double yellow center line
        Box(road, stripe, "Center Line A", new Vector3(0f, 0.015f, 17.32f), new Vector3(240f, 0.03f, 0.14f));
        Box(road, stripe, "Center Line B", new Vector3(0f, 0.015f, 17.68f), new Vector3(240f, 0.03f, 0.14f));

        // Dashed lane dividers (two lanes each direction)
        for (int i = 0; i < 48; i++)
        {
            float x = -118f + i * 5f;
            Box(road, stripe, "Lane Dash", new Vector3(x, 0.015f, 13.75f), new Vector3(2.2f, 0.03f, 0.16f));
            Box(road, stripe, "Lane Dash", new Vector3(x, 0.015f, 21.25f), new Vector3(2.2f, 0.03f, 0.16f));
        }

        // Abandoned cars (wider + taller than v1)
        BuildCar(road, new Vector3(-34f, 0f, 14f), 12f, carRed, glass);
        BuildCar(road, new Vector3(-22f, 0f, 21f), 95f, carWhite, glass);
        BuildCar(road, new Vector3(-10f, 0f, 16f), -8f, carTaxi, glass);
        BuildCar(road, new Vector3(-2f, 0f, 22f), 170f, carBlue, glass);
        BuildCar(road, new Vector3(9f, 0f, 13.5f), 24f, carWhite, glass);
        BuildCar(road, new Vector3(18f, 0f, 22f), -100f, carTaxi, glass);
        BuildCar(road, new Vector3(30f, 0f, 18f), 5f, carRed, glass);
        BuildCar(road, new Vector3(40f, 0f, 13f), 60f, carBlue, glass);
        BuildCar(road, new Vector3(-55f, 0f, 19f), 80f, carTaxi, glass);
        BuildCar(road, new Vector3(55f, 0f, 15f), -15f, carWhite, glass);

        // --- Street canyons (zombie spawn routes) at x = -22, 6, 36 -----------
        float[] streetX = { -22f, 6f, 36f };
        foreach (float sx in streetX)
        {
            Box(city, asphalt, "Street Canyon", new Vector3(sx, 0.0f, 55f), new Vector3(7f, 0.04f, 60f));
        }

        // --- City: waterfront row (z0 = 30) ------------------------------------
        BuildBuilding(city, brick, -46f, 14f, 16f, 22f, 30f);
        BuildBuilding(city, greyBlock, -32f, 10f, 14f, 16f, 30f);
        // street gap at x ~ -22
        BuildBuilding(city, tanBlock, -12f, 12f, 16f, 28f, 30f);
        BuildBuilding(city, brick, -1f, 8f, 14f, 14f, 30f);
        // street gap at x ~ 6
        BuildBuilding(city, greyBlock, 14f, 10f, 16f, 20f, 30f);
        BuildBuilding(city, tanBlock, 26f, 8f, 12f, 12f, 30f);
        // street gap at x ~ 36
        BuildBuilding(city, brick, 44f, 10f, 16f, 24f, 30f);
        BuildBuilding(city, greyBlock, 56f, 12f, 14f, 18f, 30f);
        BuildBuilding(city, tanBlock, -58f, 10f, 14f, 15f, 30f);
        BuildBuilding(city, brick, -70f, 12f, 16f, 20f, 30f);
        BuildBuilding(city, tanBlock, 68f, 12f, 16f, 26f, 30f);

        // --- City: second row (z0 = 52), taller ---------------------------------
        BuildBuilding(city, greyBlock, -52f, 16f, 18f, 34f, 52f);
        BuildBuilding(city, tanBlock, -34f, 12f, 16f, 26f, 52f);
        BuildBuilding(city, brick, -10f, 14f, 18f, 38f, 52f);
        BuildBuilding(city, greyBlock, -2f, 8f, 14f, 22f, 52f);
        BuildBuilding(city, tanBlock, 16f, 12f, 16f, 30f, 52f);
        BuildBuilding(city, brick, 28f, 10f, 14f, 24f, 52f);
        BuildBuilding(city, greyBlock, 46f, 14f, 18f, 36f, 52f);
        BuildBuilding(city, tanBlock, 60f, 10f, 14f, 28f, 52f);
        BuildBuilding(city, brick, -66f, 12f, 16f, 30f, 52f);

        // --- City: distant towers (z0 = 85), skyline silhouette ------------------
        BuildBuilding(city, darkTower, -30f, 18f, 20f, 52f, 85f);
        BuildBuilding(city, darkTower, 0f, 16f, 18f, 60f, 85f);
        BuildBuilding(city, darkTower, 30f, 20f, 20f, 48f, 85f);
        BuildBuilding(city, darkTower, 60f, 16f, 18f, 44f, 85f);
        BuildBuilding(city, darkTower, -60f, 16f, 18f, 40f, 85f);

        // --- Bridge (HQ / colony space) -------------------------------------------
        Box(bridge, asphalt, "Bridge Deck", new Vector3(0f, -0.17f, -40f), new Vector3(12f, 0.34f, 100f));
        Box(bridge, hqDeck, "HQ Zone Overlay", new Vector3(0f, 0.025f, -16f), new Vector3(11.6f, 0.05f, 38f));

        Box(bridge, concrete, "Left Curb", new Vector3(-6.1f, 0.31f, -40f), new Vector3(0.3f, 0.62f, 100f));
        Box(bridge, concrete, "Right Curb", new Vector3(6.1f, 0.31f, -40f), new Vector3(0.3f, 0.62f, 100f));
        Box(bridge, concrete, "Left Rail", new Vector3(-6.1f, 1.2f, -40f), new Vector3(0.18f, 0.16f, 100f));
        Box(bridge, concrete, "Right Rail", new Vector3(6.1f, 1.2f, -40f), new Vector3(0.18f, 0.16f, 100f));

        for (int i = 0; i < 10; i++)
        {
            float z = 5f - i * 10f;
            Box(bridge, concrete, "Left Rail Post", new Vector3(-6.1f, 0.9f, z), new Vector3(0.3f, 1.3f, 0.25f));
            Box(bridge, concrete, "Right Rail Post", new Vector3(6.1f, 0.9f, z), new Vector3(0.3f, 1.3f, 0.25f));
        }

        for (int i = 0; i < 5; i++)
        {
            float z = 0f - i * 20f;
            Cylinder(bridge, concrete, "Pylon", new Vector3(-4.5f, -1.7f, z), new Vector3(1.2f, 1.7f, 1.2f));
            Cylinder(bridge, concrete, "Pylon", new Vector3(4.5f, -1.7f, z), new Vector3(1.2f, 1.7f, 1.2f));
        }

        Sphere(bridge, glow, "South Marker L", new Vector3(-4.5f, 2.2f, -88f), Vector3.one * 0.3f);
        Sphere(bridge, glow, "South Marker R", new Vector3(4.5f, 2.2f, -88f), Vector3.one * 0.3f);

        // --- Hesco barrier with GATE at the east end -------------------------------
        // Wall spans x -7.5..7.5 hugging past the rails so there is no climbable
        // shoulder at the ends. Gate opening: x 4.0..6.2.
        for (int i = 0; i < 16; i++)
        {
            float x = -7.5f + i;
            if (x > 3.5f && x < 6.7f)
            {
                continue; // gate opening
            }
            Box(barrier, hescoSand, "Hesco Lower", new Vector3(x, 0.55f, 8.5f), new Vector3(0.96f, 1.1f, 1.1f));
            Box(barrier, hescoFace, "Hesco Face", new Vector3(x, 0.55f, 9.08f), new Vector3(0.9f, 0.95f, 0.05f));
            Box(barrier, hescoSand, "Hesco Upper", new Vector3(x, 1.65f, 8.45f), new Vector3(0.92f, 1.0f, 1.0f));
        }

        // Gate: steel posts, lintel, and a closed door panel. The door is a single
        // named object so Milestone 2 can give it a component and swing it open for
        // squad runs. Gate health IS barrier health — one pool, per design.
        // Geometry rule: every piece has clear air from its neighbors or deliberately
        // penetrates them — never shared/coplanar faces (that causes z-fighting).
        Box(gate, steel, "Gate Post West", new Vector3(4.25f, 1.3f, 8.5f), new Vector3(0.4f, 2.6f, 1.1f));
        Box(gate, steel, "Gate Post East", new Vector3(6.75f, 1.3f, 8.5f), new Vector3(0.4f, 2.6f, 1.1f));
        Box(gate, steel, "Gate Lintel", new Vector3(5.5f, 2.5f, 8.5f), new Vector3(2.9f, 0.25f, 1.0f));
        Box(gate, steel, "Gate Door", new Vector3(5.5f, 1.18f, 8.6f), new Vector3(2.3f, 2.3f, 0.12f));

        // Firing step: top at 1.5m, flush against the wall, shortened at the east
        // end to leave a deck-level corridor to the gate (x ~3.5..6).
        Box(barrier, concrete, "Firing Step", new Vector3(-2.05f, 0.75f, 6.95f), new Vector3(10.9f, 1.5f, 2.2f));
        GameObject ramp = Box(barrier, concrete, "Ramp", new Vector3(-2.05f, 0.7f, 3.4f), new Vector3(3f, 0.2f, 5.2f));
        ramp.transform.rotation = Quaternion.Euler(-17f, 0f, 0f);

        // --- Player rig --------------------------------------------------------------
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0.2f, -3f);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.slopeLimit = 46f;
        controller.stepOffset = 0.4f;

        GameObject cameraObject = new GameObject("Player Camera");
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.68f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 68f;
        camera.nearClipPlane = 0.1f; // 0.05 wrecked depth precision -> z-fighting at range
        camera.farClipPlane = 400f;
        camera.tag = "MainCamera";
        cameraObject.AddComponent<AudioListener>();

        player.AddComponent<PlayerController>();

        // --- Weapon: AKM viewmodel under the camera --------------------------------
        WeaponController weapon = cameraObject.AddComponent<WeaponController>();

        Transform muzzleTip = new GameObject("Muzzle Tip").transform;
        muzzleTip.SetParent(cameraObject.transform, false);
        muzzleTip.localPosition = MuzzleTipFallbackLocal;

        GameObject akmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AkmPrefabPath);
        if (akmPrefab != null)
        {
            GameObject akm = (GameObject)PrefabUtility.InstantiatePrefab(akmPrefab);
            akm.name = "AKM Viewmodel";
            akm.transform.SetParent(cameraObject.transform, false);
            akm.transform.localPosition = Vector3.zero;
            akm.transform.localRotation = Quaternion.identity;
            akm.transform.localScale = Vector3.one;

            foreach (Collider akmCollider in akm.GetComponentsInChildren<Collider>())
            {
                Object.DestroyImmediate(akmCollider);
            }

            // The pack ships a Built-in "Standard (Specular setup)" material, which
            // URP can't render. Build our own URP material from its textures and
            // hard-assign it - deterministic, survives every rebuild.
            Material akmMaterial = BuildAkmUrpMaterial();
            foreach (Renderer akmRenderer in akm.GetComponentsInChildren<Renderer>())
            {
                akmRenderer.sharedMaterial = akmMaterial;
            }

            // Auto-fit: align the barrel (longest axis) to +Z, scale to a realistic
            // length, then mount low-right of the camera by the measured center.
            Bounds bounds = CalculateBoundsInSpace(akm.transform, cameraObject.transform);
            Vector3 size = bounds.size;
            if (size.x >= size.y && size.x >= size.z)
            {
                akm.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            }
            else if (size.y > size.x && size.y > size.z)
            {
                akm.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            if (AkmFlip180)
            {
                akm.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * akm.transform.localRotation;
            }

            bounds = CalculateBoundsInSpace(akm.transform, cameraObject.transform);
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (longest > 0.0001f)
            {
                akm.transform.localScale = Vector3.one * (RifleTargetLength / longest);
            }

            bounds = CalculateBoundsInSpace(akm.transform, cameraObject.transform);
            akm.transform.localPosition += RifleMountLocal - bounds.center;

            bounds = CalculateBoundsInSpace(akm.transform, cameraObject.transform);
            muzzleTip.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);

            weapon.rifleModel = akm.transform;
        }
        else
        {
            Debug.LogWarning($"Dead Wave: AKM prefab not found at {AkmPrefabPath}; no viewmodel attached.");
        }

        Light muzzleLight = muzzleTip.gameObject.AddComponent<Light>();
        muzzleLight.type = LightType.Point;
        muzzleLight.range = 5f;
        muzzleLight.intensity = 0f;
        muzzleLight.color = new Color(1f, 0.72f, 0.35f);

        weapon.muzzleLight = muzzleLight;
        weapon.muzzleTip = muzzleTip;

        // --- Zombie spawn points (inside the street canyons) -------------------------
        Transform spawnGroup = NewGroup("Spawn Points", world);
        Transform[] spawnPoints = new Transform[streetX.Length];
        for (int i = 0; i < streetX.Length; i++)
        {
            GameObject spawnPoint = new GameObject($"Spawn Canyon {i + 1}");
            spawnPoint.transform.SetParent(spawnGroup, false);
            spawnPoint.transform.localPosition = new Vector3(streetX[i], 0.1f, 55f);
            spawnPoints[i] = spawnPoint.transform;
        }

        // --- Game systems --------------------------------------------------------------
        GameObject systems = new GameObject("Game Systems");
        BarrierHealth barrierHealth = systems.AddComponent<BarrierHealth>();
        ZombieSpawner spawner = systems.AddComponent<ZombieSpawner>();
        spawner.barrier = barrierHealth;
        spawner.spawnPoints = spawnPoints;
        spawner.walkerPrefab = AssetZombieBuilder.EnsureWalkerPrefab();
        systems.AddComponent<HudController>();

        // --- NavMesh bake (after all geometry exists) -----------------------------------
        GameObject navigation = new GameObject("Navigation");
        NavMeshSurface surface = navigation.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        if (surface.navMeshData != null)
        {
            AssetDatabase.CreateAsset(surface.navMeshData, "Assets/Scenes/DeadWave_Stage_NavMesh.asset");
        }
        else
        {
            Debug.LogWarning("Dead Wave: NavMesh bake produced no data.");
        }

        // --- Save ----------------------------------------------------------------------
        AssetDatabase.SaveAssets();
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (saved)
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"Dead Wave: stage v2 built and saved to {ScenePath}. Press Play and walk it.");
        }
        else
        {
            Debug.LogError($"Dead Wave: failed to save scene to {ScenePath}.");
        }
    }

    // --- Helpers ---------------------------------------------------------------

    private static Transform NewGroup(string name, Transform parent)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    /// <summary>
    /// Combined renderer bounds of a hierarchy, expressed in another transform's
    /// local space (corner-accurate, handles any rotation/scale).
    /// </summary>
    private static Bounds CalculateBoundsInSpace(Transform target, Transform space)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);
        bool hasAny = false;

        foreach (Renderer renderer in renderers)
        {
            Bounds worldBounds = renderer.bounds;
            Vector3 extents = worldBounds.extents;
            for (int xi = -1; xi <= 1; xi += 2)
            {
                for (int yi = -1; yi <= 1; yi += 2)
                {
                    for (int zi = -1; zi <= 1; zi += 2)
                    {
                        Vector3 corner = worldBounds.center + Vector3.Scale(extents, new Vector3(xi, yi, zi));
                        Vector3 local = space.InverseTransformPoint(corner);
                        if (!hasAny)
                        {
                            result = new Bounds(local, Vector3.zero);
                            hasAny = true;
                        }
                        else
                        {
                            result.Encapsulate(local);
                        }
                    }
                }
            }
        }

        return result;
    }

    private static GameObject Box(Transform parent, Material material, string name, Vector3 position, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.isStatic = true;
        return go;
    }

    private static GameObject Sphere(Transform parent, Material material, string name, Vector3 position, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.isStatic = true;
        return go;
    }

    private static GameObject Cylinder(Transform parent, Material material, string name, Vector3 position, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.isStatic = true;
        return go;
    }

    private static void BuildBuilding(Transform parent, Material material, float centerX, float sizeX, float sizeZ, float height, float z0)
    {
        if (sizeX <= 0f || height <= 0f)
        {
            return; // skip placeholder entries
        }
        float centerZ = z0 + sizeZ * 0.5f;
        Box(parent, material, "Building", new Vector3(centerX, height * 0.5f, centerZ), new Vector3(sizeX, height, sizeZ));
    }

    private static void BuildCar(Transform parent, Vector3 position, float yRotation, Material paint, Material glass)
    {
        Transform car = new GameObject("Abandoned Car").transform;
        car.SetParent(parent, false);
        car.localPosition = position;
        car.localRotation = Quaternion.Euler(0f, yRotation, 0f);

        Box(car, paint, "Body", new Vector3(0f, 0.575f, 0f), new Vector3(4.6f, 1.15f, 2.05f));
        Box(car, glass, "Cabin", new Vector3(-0.3f, 1.45f, 0f), new Vector3(2.3f, 0.6f, 1.8f));
        car.gameObject.isStatic = true;
    }

    private static Material GetOrCreateMaterial(string name, Color color, float smoothness = 0.15f, bool emissive = false)
    {
        string path = $"Assets/Materials/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("Dead Wave: URP Lit shader not found. Is URP installed?");
                shader = Shader.Find("Standard");
            }
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * 2.5f);
            }
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateSkybox()
    {
        const string path = "Assets/Materials/Sky_Procedural.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_Exposure"))
        {
            material.SetFloat("_Exposure", 1.15f);
        }
        if (material.HasProperty("_SkyTint"))
        {
            material.SetColor("_SkyTint", new Color(0.55f, 0.65f, 0.78f));
        }
        if (material.HasProperty("_SunSize"))
        {
            material.SetFloat("_SunSize", 0.035f);
        }
        if (material.HasProperty("_SunSizeConvergence"))
        {
            material.SetFloat("_SunSizeConvergence", 5f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material BuildAkmUrpMaterial()
    {
        const string textureFolder = "Assets/DelthorGames/AKM/Textures";

        Material material = GetOrCreateMaterial("AKM_URP", Color.white, 1f);
        material.SetFloat("_Metallic", 1f); // scaled by the metallic-smoothness map

        Texture albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureFolder}/AKMDis_Mat_AlbedoTransparency.png");
        Texture normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureFolder}/AKMDis_Mat4K_NormalOpenGLFix.png");
        Texture metallicSmooth = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureFolder}/AKMDis_Mat_MetallicSmoothness.png");
        Texture occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureFolder}/AKMDis_Mat_AO.png");
        Texture emission = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureFolder}/AKMDis_Mat_Emission.png");

        if (albedo != null)
        {
            material.SetTexture("_BaseMap", albedo);
        }
        if (normal != null)
        {
            EnsureNormalMapImport($"{textureFolder}/AKMDis_Mat4K_NormalOpenGLFix.png");
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        if (metallicSmooth != null)
        {
            material.SetTexture("_MetallicGlossMap", metallicSmooth);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        if (occlusion != null)
        {
            material.SetTexture("_OcclusionMap", occlusion);
        }
        if (emission != null)
        {
            material.SetTexture("_EmissionMap", emission);
            material.SetColor("_EmissionColor", Color.white);
            material.EnableKeyword("_EMISSION");
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureNormalMapImport(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
