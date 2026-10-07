using System.Linq;
using System.IO;
using UnityEditor;  
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class MotherSceneSetup
{
    private const float MotherScale = 3.5f;
    private const string ScenePath = "Assets/_Project/Scenes/SampleScene.unity";
    private const string ModelPath = "Assets/_Project/Art/Enemies/MotherMosterPSX/MotherMosterPSX.fbx";
    private const string ControllerPath = "Assets/_Project/Animations/Enemies/MotherMoster/MotherMoster.controller";
    private const string MaterialPath = "Assets/_Project/Art/Enemies/MotherMosterPSX/M_MotherMosterPSX.mat";
    private const string SetupRequest = "Logs/MotherSceneSetup.request";

    [InitializeOnLoadMethod]
    private static void ScheduleRequestedSetup()
    {
        if (Application.isBatchMode || !File.Exists(SetupRequest)) return;
        EditorApplication.update -= ApplyRequestedSetup;
        EditorApplication.update += ApplyRequestedSetup;
    }

    private static void ApplyRequestedSetup()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        EditorApplication.update -= ApplyRequestedSetup;
        try
        {
            if (File.ReadAllText(SetupRequest).Contains("Scale Mother")) ScaleExistingMother();
            else PutInSampleScene();
            File.Delete(SetupRequest);
            File.WriteAllText("Logs/MotherSceneSetup.result", "Mother setup complete in open scene. Saved=" + !scene.isDirty);
        }
        catch (System.Exception error)
        {
            Debug.LogException(error);
            File.WriteAllText("Logs/MotherSceneSetup.result", error.ToString());
        }
    }

    private static void ScaleExistingMother()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        var mother = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MotherAI>(true)).FirstOrDefault();
        if (mother == null) throw new System.InvalidOperationException("MotherMoster - Boss was not found in the open scene.");
        var model = mother.GetComponentsInChildren<Transform>(true).FirstOrDefault(t =>
            t.name == "MotherMoster" || t.name == "MotherMosterPSX" || t.name == "MotherMoster_Animated");
        if (model == null) throw new System.InvalidOperationException("Mother model child was not found.");
        model.localScale = Vector3.one * MotherScale;
        var agent = mother.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.height = 2.2f * MotherScale;
            agent.radius = .55f * MotherScale;
            agent.stoppingDistance = 2.2f * MotherScale;
        }
        var collider = mother.GetComponent<CapsuleCollider>();
        if (collider != null)
        {
            collider.center = new Vector3(0, MotherScale, 0);
            collider.radius = .55f * MotherScale;
            collider.height = 2.2f * MotherScale;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = mother.gameObject;
        Debug.Log("MotherMoster scaled to 3.5x with matching collider and NavMeshAgent.", mother.gameObject);
    }

    [MenuItem("Tools/Monster/Setup Mother In Sample Scene")]
    public static void PutInSampleScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Stop Play mode before setting up Mother.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        bool hadUnsavedChanges = scene.isDirty;
        var spawner = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EnemySpawner>(true)).FirstOrDefault();
        if (spawner == null) throw new System.InvalidOperationException("EnemySpawner is missing from SampleScene.");

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (modelAsset == null || controller == null)
            throw new System.InvalidOperationException("Mother FBX or controller is missing. Run Build MotherMoster Animations first.");

        var transforms = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        var existing = transforms.FirstOrDefault(t => t.GetComponent<MotherAI>() != null
            || t.name == "MotherMoster - Boss");
        var modelTransform = transforms.FirstOrDefault(t =>
            (t.name == "MotherMosterPSX" || t.name == "MotherMoster" || t.name == "MotherMoster_Animated"
             || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == ModelPath)
            && t.GetComponentInChildren<Animator>(true) != null
            && (existing == null || t.IsChildOf(existing)));
        GameObject enemy;
        if (existing != null) enemy = existing.gameObject;
        else
        {
            enemy = new GameObject("MotherMoster - Boss");
            Undo.RegisterCreatedObjectUndo(enemy, "Setup Mother");
            SceneManager.MoveGameObjectToScene(enemy, scene);
            enemy.transform.SetPositionAndRotation(
                modelTransform != null ? modelTransform.position : new Vector3(3.2f, 1.5f, -0.47f),
                modelTransform != null ? modelTransform.rotation : Quaternion.identity);
            if (modelTransform != null)
            {
                enemy.transform.SetParent(modelTransform.parent, true);
                Undo.SetTransformParent(modelTransform, enemy.transform, "Group Mother model");
            }
        }
        var model = modelTransform != null ? modelTransform.gameObject
            : (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, enemy.transform);
        if (modelTransform == null)
        {
            Undo.RegisterCreatedObjectUndo(model, "Setup Mother model");
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
        }
        model.name = "MotherMoster";
        model.transform.localScale = Vector3.one * MotherScale;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (material != null) renderer.sharedMaterial = material;
            if (renderer is SkinnedMeshRenderer skinned)
                skinned.localBounds = new Bounds(new Vector3(0, .65f, 0), new Vector3(3, 3, 3));
        }
        var animator = model.GetComponentInChildren<Animator>(true);
        if (animator == null) animator = Undo.AddComponent<Animator>(model);
        RepairAnimationBindings(modelAsset, model, animator, controller);
        animator.runtimeAnimatorController = controller;
        animator.enabled = true;
        animator.speed = 1f;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (animator.GetComponent<MonsterAttackEvents>() == null)
            Undo.AddComponent<MonsterAttackEvents>(animator.gameObject);

        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null) agent = Undo.AddComponent<NavMeshAgent>(enemy);
        agent.height = 2.2f * MotherScale; agent.radius = .55f * MotherScale; agent.speed = 3.8f;
        agent.acceleration = 18f; agent.angularSpeed = 540f; agent.stoppingDistance = 2.2f * MotherScale;
        if (enemy.GetComponent<MotherStagger>() == null) Undo.AddComponent<MotherStagger>(enemy);
        if (enemy.GetComponent<MotherAI>() == null) Undo.AddComponent<MotherAI>(enemy);
        var collider = enemy.GetComponent<CapsuleCollider>();
        if (collider == null) collider = Undo.AddComponent<CapsuleCollider>(enemy);
        collider.center = new Vector3(0, 1f * MotherScale, 0); collider.radius = .55f * MotherScale; collider.height = 2.2f * MotherScale;
        enemy.GetComponent<MotherAI>().SetTarget(scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerHealth>(true)).FirstOrDefault());

        EditorSceneManager.MarkSceneDirty(scene);
        // Keep the user's unrelated in-memory scene edits available for review.
        if (!hadUnsavedChanges) EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("MotherMoster added to SampleScene with Animator, MotherAI, NavMeshAgent and stagger hit handling.");
    }

    private static void RepairAnimationBindings(GameObject modelAsset, GameObject model,
        Animator animator, RuntimeAnimatorController runtimeController)
    {
        var controller = runtimeController as AnimatorController;
        if (controller == null) throw new System.InvalidOperationException("Mother requires an AnimatorController.");
        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__")).ToArray();
        var report = new System.Text.StringBuilder();
        report.AppendLine("Animator object: " + animator.name);
        var sourceAnimator = modelAsset.GetComponentInChildren<Animator>(true);
        if (sourceAnimator != null) animator.avatar = sourceAnimator.avatar;
        foreach (var child in controller.layers[0].stateMachine.states)
        {
            var state = child.state;
            var clip = clips.FirstOrDefault(c => c.name == state.name
                || c.name.EndsWith("_" + state.name, System.StringComparison.Ordinal));
            if (clip == null)
                throw new System.InvalidOperationException("Mother animation clip missing: " + state.name);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            int matching = bindings.Count(binding => string.IsNullOrEmpty(binding.path)
                || animator.transform.Find(binding.path) != null);
            report.AppendLine($"{state.name}: length={clip.length:F3}s, curves={bindings.Length}, matching paths={matching}");
            if (clip.length <= 0 || bindings.Length == 0 || matching == 0)
            {
                File.WriteAllText("Logs/MotherAnimationValidation.txt", report.ToString());
                throw new System.InvalidOperationException("Mother clip has no animation matching the model: " + state.name);
            }
            // Resolve imported sub-assets through Unity rather than assuming FBX file IDs.
            state.motion = state.name == "Idle" ? MotherIdleAnimation.Build(clip) : clip;
            state.speed = 1f;
            EditorUtility.SetDirty(state);
        }
        EditorUtility.SetDirty(controller);
        File.WriteAllText("Logs/MotherAnimationValidation.txt", report.ToString());
    }
}
