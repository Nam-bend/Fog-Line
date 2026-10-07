using UnityEngine;

// All story items and the temporary Briggs actor use the same cube prefab shape.
public sealed class ForestStoryItem : Interactable
{
    public string id;
    public string title;
    public Vector3 groundPoint;
    public bool Used => ForestStoryDirector.Instance != null && ForestStoryDirector.Instance.Has(id);

    protected override void Interact()
    {
        if (ForestStoryDirector.Instance != null) ForestStoryDirector.Instance.Interact(this);
    }
}
