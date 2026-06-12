using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports Asset Store packages from the local download cache into the project.
/// Menu: Dead Wave > Assets > ...
/// Packages are imported non-interactively (everything in the package comes in).
/// </summary>
public static class DeadWaveAssetImporter
{
    private static readonly string CacheRoot = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "Unity", "Asset Store-5.x");

    private static readonly string[] MilestoneOnePackages =
    {
        @"Pxltiger\3D ModelsCharactersHumanoids\Zombie.unitypackage",
        @"Delthor Games\3D ModelsPropsWeaponsGuns\Free FPS Weapon - AKM.unitypackage",
        @"Kevin Iglesias\Animation\Human Basic Motions FREE.unitypackage",
    };

    [MenuItem("Dead Wave/Assets/Import Milestone 1 Assets")]
    public static void ImportMilestoneOne()
    {
        foreach (string relativePath in MilestoneOnePackages)
        {
            string fullPath = Path.Combine(CacheRoot, relativePath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"Dead Wave: package not found in cache: {fullPath}");
                continue;
            }

            Debug.Log($"Dead Wave: importing {Path.GetFileName(fullPath)} ...");
            AssetDatabase.ImportPackage(fullPath, false);
        }

        Debug.Log("Dead Wave: imports queued. When Unity finishes, run " +
                  "'Dead Wave > Assets > Upgrade Imported Materials to URP'.");
    }

    [MenuItem("Dead Wave/Assets/Fix Imported Materials (Standard to URP)")]
    public static void FixImportedMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("Dead Wave: URP Lit shader not found.");
            return;
        }

        int converted = 0;
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null || !mat.shader.name.StartsWith("Standard"))
            {
                continue;
            }

            // Capture Built-in Standard properties before the shader switch wipes them.
            Texture albedo = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            Texture metallic = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            Texture occlusion = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
            Texture emission = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
            Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            float smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            float metallicValue = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;

            mat.shader = urpLit;

            mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallicValue);

            if (bump != null)
            {
                mat.SetTexture("_BumpMap", bump);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (metallic != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (occlusion != null)
            {
                mat.SetTexture("_OcclusionMap", occlusion);
            }
            if (emission != null || emissionColor.maxColorComponent > 0.01f)
            {
                mat.SetTexture("_EmissionMap", emission);
                mat.SetColor("_EmissionColor", emissionColor);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(mat);
            converted++;
            Debug.Log($"Dead Wave: converted to URP Lit: {path}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Dead Wave: material fix complete - {converted} material(s) converted.");
    }

    [MenuItem("Dead Wave/Assets/Upgrade Imported Materials to URP")]
    public static void UpgradeMaterials()
    {
        bool executed = EditorApplication.ExecuteMenuItem(
            "Edit/Rendering/Materials/Convert All Built-in Materials to URP");

        if (!executed)
        {
            Debug.LogError("Dead Wave: URP conversion menu not found at the expected path. " +
                           "Open Window > Rendering > Render Pipeline Converter and run " +
                           "'Material Upgrade' manually.");
        }
    }
}
