using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.IO;

public static class FirstPersonWeaponSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/SampleScene.unity";
    private const string ModelPath = "Assets/_Project/Art/Weapons/TraditionalDoubleBarrel/Models/TraditionalDoubleBarrel.fbx";

    public static void Setup()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        PlayerLook player = Object.FindFirstObjectByType<PlayerLook>();
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (player == null || player.cam == null || modelAsset == null)
            throw new System.InvalidOperationException("Player camera or shotgun model is missing.");

        Transform oldView = player.cam.transform.Find("WeaponView");
        if (oldView != null) Object.DestroyImmediate(oldView.gameObject);

        GameObject view = new GameObject("WeaponView");
        view.transform.SetParent(player.cam.transform, false);
        view.transform.localPosition = new Vector3(0.20f, -0.50f, 0.85f);
        view.transform.localRotation = Quaternion.Euler(-3f, -7f, 0f);

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, view.transform);
        model.name = "TraditionalDoubleBarrel";
        model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
        model.transform.localScale = Vector3.one * 0.18f;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        FirstPersonWeapon weapon = player.GetComponent<FirstPersonWeapon>();
        if (weapon == null) weapon = player.gameObject.AddComponent<FirstPersonWeapon>();
        SerializedObject serialized = new SerializedObject(weapon);
        serialized.FindProperty("weaponModel").objectReferenceValue = model;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Shotgun is now permanently visible under Main Camera/WeaponView.");
    }

    public static void CaptureCurrentView()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = Object.FindFirstObjectByType<PlayerLook>().cam;
        RenderTexture target = new RenderTexture(1280, 720, 24);
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        string output = Path.GetFullPath("Temp/WeaponViewCheck.png");
        File.WriteAllBytes(output, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(image);
        Debug.Log($"Weapon view captured at {output}");
    }
}
