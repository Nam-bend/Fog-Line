using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public sealed class ForestDestination : MonoBehaviour
{
    public UnityEvent onReached = new UnityEvent();
    public bool Reached { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        if (Reached || other.GetComponentInParent<PlayerHealth>() == null) return;
        Reached = true;
        onReached.Invoke();
        Debug.Log("Forest demo complete: ranger cabin B reached.", this);
    }

    private void OnGUI()
    {
        if (!Reached) return;
        GUI.Box(new Rect(Screen.width / 2f - 180, 40, 360, 55),
            "DA DEN LAN KIEM LAM - DIEM B\nHoan thanh demo rung");
    }
}
