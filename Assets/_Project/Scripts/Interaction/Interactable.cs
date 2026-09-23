using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    //message displayed to player when looking at interactable
    public string promptMessage;

    //function will be called by the player
    public void BaseInteract(){
       
       Interact();
    }

    protected virtual void Interact(){}
    



}
