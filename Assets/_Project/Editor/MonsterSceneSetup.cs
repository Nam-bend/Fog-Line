using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.Animations;
using System.Linq;
using UnityEngine.SceneManagement;

public static class MonsterSceneSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/SampleScene.unity";
    private const string ModelPath = "Assets/_Project/Art/Enemies/MonsterPSX/MonsterPSX.fbx";
    private const string MaterialPath = "Assets/_Project/Art/Enemies/MonsterPSX/M_MonsterPSX.mat";
    private const string ControllerPath = "Assets/_Project/Animations/Enemies/MonsterPSX.controller";

    // Finish the previously requested scene setup once, in edit mode, after compilation.
    [InitializeOnLoadMethod]
    private static void ScheduleMissingControllerSetup()
    {
        EditorApplication.update -= ApplyMissingController;
        EditorApplication.update += ApplyMissingController;
    }

    private static void ApplyMissingController()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
        {
            EditorApplication.update -= ApplyMissingController;
            return;
        }
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        EditorApplication.update -= ApplyMissingController;
        ReplaceSceneEnemy();
    }

    [MenuItem("Tools/Monster/Replace Scene Enemy With MonsterPSX")]
    private static void ReplaceSceneEnemyMenu() => ReplaceSceneEnemy();

    public static void ReplaceSceneEnemy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Stop Play mode before saving the monster setup.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var spawner = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemySpawner>(true)).FirstOrDefault();
        if (spawner == null) throw new System.InvalidOperationException("EnemySpawner is missing.");

        var serialized = new SerializedObject(spawner);
        var enemy = serialized.FindProperty("sceneEnemy").objectReferenceValue as GameObject;
        if (enemy == null) throw new System.InvalidOperationException("Scene enemy is missing.");

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (modelAsset == null) throw new System.InvalidOperationException("MonsterPSX FBX is missing.");

        Transform existing = enemy.transform.Find("MonsterPSX");
        var model = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, enemy.transform);
        model.name = "MonsterPSX";
        if (existing == null)
        {
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
        }
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        AssignMonsterAnimator(model);
        Transform oldBody = enemy.transform.Find("Body");
        if (oldBody != null) Object.DestroyImmediate(oldBody.gameObject);

        var collider = enemy.GetComponent<CapsuleCollider>();
        if (collider == null) collider = enemy.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 1f, 0f);
        collider.radius = 0.5f;
        collider.height = 2f;
        collider.direction = 1;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("MonsterPSX setup saved: scene Animator has Idle, Run and Attack; attack damage uses an animation event.");
    }

    private static void AssignMonsterAnimator(GameObject model)
    {
        string folder = "Assets/_Project/Animations/Enemies";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Animations"))
                AssetDatabase.CreateFolder("Assets/_Project", "Animations");
            AssetDatabase.CreateFolder("Assets/_Project/Animations", "Enemies");
        }

        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();
        if (clips.Length == 0) throw new System.InvalidOperationException("MonsterPSX has no animation clips.");

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var stateMachine = controller.layers[0].stateMachine;
        foreach (var transition in stateMachine.anyStateTransitions)
            stateMachine.RemoveAnyStateTransition(transition);
        foreach (var child in stateMachine.states) stateMachine.RemoveState(child.state);
        var idle = SaveClip(clips.First(c => c.name.Contains("Idle_Watchful")), "Idle", true);
        var run = SaveClip(clips.First(c => c.name.Contains("Run_Frantic")), "Run", true);
        var attack = SaveClip(clips.First(c => c.name.Contains("Attack_Lunge")), "Attack", false);
        var idleState = stateMachine.AddState("Idle");
        idleState.motion = idle;
        var runState = stateMachine.AddState("Run");
        runState.motion = run;
        var attackState = stateMachine.AddState("Attack");
        attackState.motion = attack;
        stateMachine.defaultState = idleState;
        if (!controller.parameters.Any(p => p.name == "IsMoving"))
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        var toRun = idleState.AddTransition(runState);
        toRun.hasExitTime = false;
        toRun.duration = 0.12f;
        toRun.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
        var toIdle = runState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.12f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
        if (!controller.parameters.Any(p => p.name == "Attack"))
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        var toAttack = stateMachine.AddAnyStateTransition(attackState);
        toAttack.hasExitTime = false;
        toAttack.duration = 0.05f;
        toAttack.canTransitionToSelf = false;
        toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        var attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 1f;
        attackToIdle.duration = 0.1f;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        var animator = model.GetComponentInChildren<Animator>(true);
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.enabled = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (animator.GetComponent<MonsterAttackEvents>() == null)
            animator.gameObject.AddComponent<MonsterAttackEvents>();
        EditorUtility.SetDirty(animator);
        if (PrefabUtility.IsPartOfPrefabInstance(animator))
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
    }

    private static AnimationClip SaveClip(AnimationClip source, string name, bool loop)
    {
        string path = "Assets/_Project/Animations/Enemies/MonsterPSX_" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = Object.Instantiate(source);
            AssetDatabase.CreateAsset(clip, path);
        }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = "MonsterPSX_" + name;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, name == "Attack"
            ? new[] { new AnimationEvent { functionName = "OnMonsterStrike", time = Mathf.Min(0.4f, clip.length * 0.5f) } }
            : new AnimationEvent[0]);
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
