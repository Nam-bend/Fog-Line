using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ForestStoryState : ForestStoryProgress
{
    public int version = 1;
    public Vector3 position;
    public float yaw;
    public float health = 100;
    public int left, right, reserve = 24;
    public List<ForestEnemySnapshot> enemies = new List<ForestEnemySnapshot>();
}

[Serializable]
public sealed class ForestEnemySnapshot
{
    public string id;
    public float health;
    public Vector3 position;
}
