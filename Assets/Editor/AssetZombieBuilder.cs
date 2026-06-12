using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds the production Walker prefab around the imported Pxltiger zombie model:
/// NavMeshAgent + Health + ZombieController on the root, the rigged model nested
/// inside with a generated Animator Controller, a body capsule for hits, and a
/// headshot SphereCollider found via the Humanoid bone map.
/// Falls back to the primitive walker if the asset is missing.
/// Menu: Dead Wave > Build Walker Prefab (Asset Model)
/// </summary>
public static class AssetZombieBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Zombie_Walker.prefab";
    private const string ControllerPath = "Assets/Prefabs/Walker_Animator.controller";
    private const string ModelPrefabPath = "Assets/Zombie/Prefabs/Zombie1.prefab";
    private const string AnimFolder = "Assets/Zombie/Animations";

    [MenuItem("Dead Wave/Build Walker Prefab (Asset Model)")]
    public static void BuildMenu()
    {
        GameObject prefab = EnsureWalkerPrefab();
        Debug.Log($"Dead Wave: walker prefab saved to {AssetDatabase.GetAssetPath(prefab)}");
    }

    public static GameObject EnsureWalkerPrefab()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
        if (model == null)
        {
            Debug.LogWarning($"Dead Wave: zombie model not found at {ModelPrefabPath}; " +
                             "building the primitive walker instead.");
            return ZombiePrefabBuilder.EnsureWalkerPrefab();
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        AnimatorController controller = BuildAnimatorController();

        GameObject root = new GameObject("Zombie_Walker");
        try
        {
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 0.95f; // matched to the walk animation's pace (no foot-slide)
            agent.acceleration = 6f;
            agent.angularSpeed = 240f;
            agent.stoppingDistance = 0.6f;
            agent.radius = 0.35f;
            agent.height = 1.8f;

            root.AddComponent<Health>();
            root.AddComponent<ZombieController>();

            // Body hit volume (regular damage).
            CapsuleCollider body = root.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 0.9f, 0f);
            body.height = 1.7f;
            body.radius = 0.3f;

            // Nested model instance keeps the link to the asset prefab.
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Model";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visual.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // the NavMeshAgent moves the body

            // Headshot collider via the Humanoid bone map, with a name-search fallback.
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
            {
                headBone = FindDeepChildContaining(visual.transform, "head");
            }

            if (headBone != null)
            {
                SphereCollider headCollider = headBone.gameObject.AddComponent<SphereCollider>();
                headCollider.radius = 0.14f;
                headBone.gameObject.AddComponent<HitZone>().damageMultiplier = 2f;
            }
            else
            {
                Debug.LogWarning("Dead Wave: no head bone found; walker headshots disabled.");
            }

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static AnimatorController BuildAnimatorController()
    {
        AnimationClip idle = LoadClip("Z_Idle");
        AnimationClip walk = LoadClip("Z_Walk_InPlace");
        AnimationClip attack = LoadClip("Z_Attack");
        AnimationClip death = LoadClip("Z_FallingBack");

        SetLooping(idle, true);
        SetLooping(walk, true);
        SetLooping(attack, true);
        SetLooping(death, false);

        AssetDatabase.DeleteAsset(ControllerPath); // rebuild fresh and deterministic
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState idleState = sm.AddState("Idle");
        idleState.motion = idle;
        AnimatorState walkState = sm.AddState("Walk");
        walkState.motion = walk;
        AnimatorState attackState = sm.AddState("Attack");
        attackState.motion = attack;
        AnimatorState deathState = sm.AddState("Death");
        deathState.motion = death;

        sm.defaultState = idleState;

        AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0.25f;
        idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0.25f;
        walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        AnimatorStateTransition anyToAttack = sm.AddAnyStateTransition(attackState);
        anyToAttack.hasExitTime = false;
        anyToAttack.duration = 0.2f;
        anyToAttack.canTransitionToSelf = false;
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "IsAttacking");

        AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = false;
        attackToIdle.duration = 0.2f;
        attackToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAttacking");

        AnimatorStateTransition anyToDeath = sm.AddAnyStateTransition(deathState);
        anyToDeath.hasExitTime = false;
        anyToDeath.duration = 0.1f;
        anyToDeath.canTransitionToSelf = false;
        anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, "Die");

        return controller;
    }

    private static AnimationClip LoadClip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimFolder}/{name}.anim");
        if (clip == null)
        {
            Debug.LogError($"Dead Wave: animation clip missing: {AnimFolder}/{name}.anim");
        }
        return clip;
    }

    private static void SetLooping(AnimationClip clip, bool loop)
    {
        if (clip == null)
        {
            return;
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime != loop)
        {
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }
    }

    private static Transform FindDeepChildContaining(Transform parent, string nameFragment)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>())
        {
            if (child.name.ToLowerInvariant().Contains(nameFragment))
            {
                return child;
            }
        }
        return null;
    }
}
