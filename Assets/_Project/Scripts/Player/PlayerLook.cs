using UnityEngine;

public class PlayerLook : MonoBehaviour
{
   
    public Camera cam ;
    private float xRotation = 0f;
    
    private float xSensitivity = 30f;
    private float ySensitivity = 30f;
    private Vector2 recoil;

    public void AddRecoil(float pitch, float yaw)
    {
        recoil.x = Mathf.Clamp(recoil.x + pitch, 0f, 5f);
        recoil.y = Mathf.Clamp(recoil.y + yaw, -2f, 2f);
    }

    public void ResetRecoil() { recoil = Vector2.zero; }
    

    public void ProcessLook(Vector2 input){
        float mouseX = input.x;
        float mouseY = input.y;
        // caculate camera rotation for looking up and down 
        xRotation -=(mouseY * Time.deltaTime) * ySensitivity;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        // apply camera rotation for looking up and down
        recoil = Vector2.Lerp(recoil, Vector2.zero, 1f - Mathf.Exp(-10f * Time.deltaTime));
        cam.transform.localRotation = Quaternion.Euler(Mathf.Clamp(xRotation - recoil.x, -85f, 85f), recoil.y, 0f);

        // apply camera rotation for looking left and right
        transform.Rotate(Vector3.up * (mouseX * Time.deltaTime) * xSensitivity);
    }
    
}
