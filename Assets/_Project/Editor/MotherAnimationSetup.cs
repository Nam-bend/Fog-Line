using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MotherAnimationSetup
{
    private const string ModelPath = "Assets/_Project/Art/Enemies/MotherMosterPSX/MotherMosterPSX.fbx";
    private const string Folder = "Assets/_Project/Animations/Enemies/MotherMoster";
    private const string PrefabPath = "Assets/_Project/Art/Enemies/MotherMosterPSX/MotherMoster_Animated.prefab";
    private static readonly string[] Names = { "Idle", "Walk", "Run", "Attack", "Stagger", "Death", "Attack_Sweep", "Attack_Combo", "Attack_Slam" };
    private static readonly string[] Attacks = { "Attack", "Attack_Sweep", "Attack_Combo", "Attack_Slam" };

    private static float[] StrikeTimes(string name) => name switch
    {
        "Attack" => new[] { .5f },
        "Attack_Sweep" => new[] { 16f / 30f },
        "Attack_Combo" => new[] { .5f, 28f / 30f },
        "Attack_Slam" => new[] { .75f },
        _ => Array.Empty<float>()
    };

    [MenuItem("Tools/Monster/Build MotherMoster Animations")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before building animation assets.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.motionNodeName = string.Empty;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        var takes = importer.defaultClipAnimations;
        if (takes.Length != Names.Length)
            throw new InvalidOperationException($"Expected {Names.Length} FBX takes, got {takes.Length}.");
        foreach (var take in takes)
        {
            string name = Names.Single(n => take.name.EndsWith("_" + n, StringComparison.Ordinal));
            take.name = name;
            take.loopTime = name == "Idle" || name == "Walk" || name == "Run";
            take.loopPose = take.loopTime;
            take.lockRootRotation = true;
            take.lockRootHeightY = true;
            take.lockRootPositionXZ = true;
            take.keepOriginalOrientation = true;
            take.keepOriginalPositionY = true;
            take.keepOriginalPositionXZ = true;
            float duration = (take.lastFrame - take.firstFrame) / 30f;
            take.events = StrikeTimes(name).Select(time => new AnimationEvent
                { time = time / duration, functionName = "OnMonsterStrike" }).ToArray();
        }
        importer.clipAnimations = takes;
        importer.SaveAndReimport();
        var imported = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
        var clips = Names.ToDictionary(n => n, n => SaveClip(imported.Single(c => c.name == n), n));
        clips["Idle"] = MotherIdleAnimation.Build(imported.Single(c => c.name == "Idle"));
        string controllerPath = Folder + "/MotherMoster.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
            ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
        foreach (var state in machine.states) machine.RemoveState(state.state);
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("AttackVariant", AnimatorControllerParameterType.Int);
        foreach (string trigger in new[] { "Attack", "Stagger", "Die" })
            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
        var states = Names.ToDictionary(n => n, n => machine.AddState(n));
        for (int i = 0; i < Names.Length; i++)
        {
            states[Names[i]].motion = clips[Names[i]];
            states[Names[i]].writeDefaultValues = false;
            if (Attacks.Contains(Names[i]))
            {
                states[Names[i]].tag = "Attack";
                states[Names[i]].AddStateMachineBehaviour<MotherAttackVariation>();
            }
        }
        machine.defaultState = states["Idle"];
        // Death is terminal. Explicit source transitions prevent triggers from reviving it.
        foreach (string name in Names.Where(n => n != "Death"))
        {
            Trigger(states[name], states["Death"], "Die", .10f);
            if (name != "Stagger") Trigger(states[name], states["Stagger"], "Stagger", .05f);
            if (!Attacks.Contains(name) && name != "Stagger")
                for (int i = 0; i < Attacks.Length; i++)
                {
                    var attack = Trigger(states[name], states[Attacks[i]], "Attack", .06f);
                    attack.AddCondition(AnimatorConditionMode.Equals, i, "AttackVariant");
                }
        }
        var idleWalk = Transition(states["Idle"], states["Walk"]);
        idleWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        idleWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
        var idleRun = Transition(states["Idle"], states["Run"]);
        idleRun.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        idleRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
        foreach (string name in new[] { "Walk", "Run" })
            Transition(states[name], states["Idle"]).AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        Transition(states["Walk"], states["Run"]).AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
        Transition(states["Run"], states["Walk"]).AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
        foreach (string name in Attacks.Concat(new[] { "Stagger" }))
        {
            var exit = Transition(states[name], states["Idle"]);
            exit.hasExitTime = true;
            exit.exitTime = 1;
            exit.duration = .08f;
        }
        EditorUtility.SetDirty(controller);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            instance.name = "MotherMoster_Animated";
            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            if (instance.GetComponent<MonsterAttackEvents>() == null) instance.AddComponent<MonsterAttackEvents>();
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Enemies/MotherMosterPSX/M_MotherMosterPSX.mat");
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.sharedMaterial = material;
                // Includes the prone death pose and wide tentacle attack.
                renderer.localBounds = new Bounds(new Vector3(0, .65f, 0), new Vector3(3, 3, 3));
            }
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            var results = Names.Select(n => ValidateClip(instance, clips[n], n)).ToArray();
            File.WriteAllLines("ArtSource/MotherMoster/unity_animation_validation.txt", results);
        }
        finally { Object.DestroyImmediate(instance); }
        AssetDatabase.SaveAssets();
        Debug.Log("MOTHER_ANIMATIONS_OK: nine clips, four alternating attacks, charge run and animated prefab saved.");
    }

    private static AnimationClip SaveClip(AnimationClip source, string name)
    {
        string path = Folder + "/MotherMoster_" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = Object.Instantiate(source);
            AssetDatabase.CreateAsset(clip, path);
        }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = "MotherMoster_" + name;
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorStateTransition Transition(AnimatorState source, AnimatorState target)
    {
        var transition = source.AddTransition(target);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = .12f;
        transition.canTransitionToSelf = false;
        return transition;
    }

    private static AnimatorStateTransition Trigger(AnimatorState source, AnimatorState target, string name, float duration)
    {
        var transition = Transition(source, target);
        transition.duration = duration;
        transition.AddCondition(AnimatorConditionMode.If, 0, name);
        return transition;
    }

    private static string ValidateClip(GameObject model, AnimationClip clip, string name)
    {
        if (clip.length <= 0 || AnimationUtility.GetCurveBindings(clip).Length == 0)
            throw new InvalidOperationException(name + " has no animation curves.");
        var transforms = model.GetComponentsInChildren<Transform>();
        clip.SampleAnimation(model, 0);
        var before = transforms.Select(t => t.localToWorldMatrix).ToArray();
        clip.SampleAnimation(model, clip.length * .43f);
        if (!transforms.Where((t, i) => t.localToWorldMatrix != before[i]).Any())
            throw new InvalidOperationException(name + " did not move the imported skeleton.");
        bool loop = name == "Idle" || name == "Walk" || name == "Run";
        if (AnimationUtility.GetAnimationClipSettings(clip).loopTime != loop)
            throw new InvalidOperationException(name + " has incorrect looping settings.");
        foreach (float time in StrikeTimes(name))
            if (!AnimationUtility.GetAnimationEvents(clip)
                .Any(e => e.functionName == "OnMonsterStrike" && Mathf.Abs(e.time - time) < .01f))
                throw new InvalidOperationException(name + " strike event is missing.");
        return $"{name}: {clip.length:F3}s, loop={loop}, curves={AnimationUtility.GetCurveBindings(clip).Length}, sampled motion OK";
    }
}
