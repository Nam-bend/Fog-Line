using System;
using System.IO;
using UnityEngine;

public static class ForestMenuFlow
{
    public const string MenuScene = "MainMenu";
    public const string GameScene = "SampleScene";
    public static bool SupportsCheckpoints => GameScene == "ForestDemo";
    public const string VolumeKey = "Blackpine.MasterVolume";
    public static string CheckpointPath => Path.Combine(Application.persistentDataPath,"blackpine-demo-v1.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyPreferences()
    {
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey,.8f));
    }

    public static bool TryReadCheckpoint(out string json, out string description)
    {
        json = null; description = "Chưa có checkpoint";
        try
        {
            if (!SupportsCheckpoints)
            {
                description = "Map cũ đã gỡ; checkpoint tạm ngưng sử dụng";
                return false;
            }
            if (!File.Exists(CheckpointPath)) return false;
            string candidate = File.ReadAllText(CheckpointPath);
            var state = JsonUtility.FromJson<ForestStoryState>(candidate);
            if (state == null || state.version != 1 || state.stage < 0 || state.stage > 3 ||
                state.clues == null || state.enemies == null || state.health <= 0 ||
                !Finite(state.position.x) || !Finite(state.position.y) || !Finite(state.position.z) ||
                !Finite(state.health) || !Finite(state.yaw))
            {
                description = "Checkpoint không hợp lệ"; return false;
            }
            json = candidate;
            string[] chapters = { "Dấu vết đầu tiên", "Đường đến trạm kỹ thuật", "Lán kiểm lâm", "Đường trở về" };
            description = chapters[state.stage] + " · " + File.GetLastWriteTime(CheckpointPath).ToString("dd/MM HH:mm");
            return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        {
            description = "Không đọc được checkpoint"; return false;
        }
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
