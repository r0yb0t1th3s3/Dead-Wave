using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds the Walker zombie prefab from primitives and saves it as an asset.
/// Menu: Dead Wave > Build Zombie Prefab (also called by the stage builder).
/// The visuals are placeholder; the component setup (NavMeshAgent, Health,
/// ZombieController, head HitZone) is the production-real part.
/// </summary>
public static class ZombiePrefabBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Zombie_Walker_Primitive.prefab";

    [MenuItem("Dead Wave/Build Zombie Prefab")]
    public static void BuildMenu()
    {
        GameObject prefab = EnsureWalkerPrefab();
        Debug.Log($"Dead Wave: walker prefab saved to {AssetDatabase.GetAssetPath(prefab)}");
    }

    public static GameObject EnsureWalkerPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        Material skin = GetOrCreateMaterial("Zombie_Skin", new Color(0.47f, 0.52f, 0.43f));
        Material cloth = GetOrCreateMaterial("Zombie_Cloth", new Color(0.28f, 0.26f, 0.24f));

        GameObject root = new GameObject("Zombie_Walker");
        try
        {
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 1.5f;
            agent.acceleration = 6f;
            agent.angularSpeed = 240f;
            agent.stoppingDistance = 0.6f;
            agent.radius = 0.35f;
            agent.height = 1.8f;

            root.AddComponent<Health>(); // default 60: two body shots or one headshot
            root.AddComponent<ZombieController>();

            // Body (torso + legs as one capsule)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial = cloth;

            // Head (2x damage HitZone)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0.05f);
            head.transform.localScale = Vector3.one * 0.42f;
            head.GetComponent<Renderer>().sharedMaterial = skin;
            head.AddComponent<HitZone>().damageMultiplier = 2f;

            // Arms, raised forward in the classic shamble
            GameObject armL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            armL.name = "Arm L";
            armL.transform.SetParent(root.transform, false);
            armL.transform.localPosition = new Vector3(-0.28f, 1.25f, 0.38f);
            armL.transform.localScale = new Vector3(0.12f, 0.12f, 0.62f);
            armL.GetComponent<Renderer>().sharedMaterial = skin;

            GameObject armR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            armR.name = "Arm R";
            armR.transform.SetParent(root.transform, false);
            armR.transform.localPosition = new Vector3(0.28f, 1.25f, 0.38f);
            armR.transform.localScale = new Vector3(0.12f, 0.12f, 0.62f);
            armR.GetComponent<Renderer>().sharedMaterial = skin;

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        string path = $"Assets/Materials/{name}.mat";
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
