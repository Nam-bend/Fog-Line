using UnityEngine;

public class PlayerMotor : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 playerVelocity;


    public float speed = 5f;
    public float gravity = -9.8f;
    public float jumpHeight = 3f;
  

       void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public void ProcessMove(Vector2 input){
        if (controller == null || !controller.enabled) return;
        Vector3 moveDirection = new Vector3(input.x, 0f, input.y);
        if(controller.isGrounded && playerVelocity.y < 0){
            playerVelocity.y = -2f;
        }
        playerVelocity.y += gravity * Time.deltaTime;
        // One collision move keeps horizontal and vertical contact in sync.
        Vector3 velocity = transform.TransformDirection(moveDirection) * speed;
        velocity.y = playerVelocity.y;
        CollisionFlags contacts = controller.Move(velocity * Time.deltaTime);
        if ((contacts & CollisionFlags.Above) != 0 && playerVelocity.y > 0f)
            playerVelocity.y = 0f;
    }


    public void Jump(){
        if(controller != null && controller.enabled && controller.isGrounded && playerVelocity.y <= 0f){
            playerVelocity.y =Mathf.Sqrt(jumpHeight * -3f* gravity);
        }

    }
}
