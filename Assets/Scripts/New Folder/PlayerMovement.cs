using Fusion;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] CharacterController ch;
    public float playerSpeed;
    public float jumpForce;
    float Gravity = -9.81f;
    Vector3 velocity;
    
    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority == false)
        {
            return;
        }
        
        if(ch.isGrounded)
        {
            velocity.y = -2f; 
        }
        else
        {
            velocity.y += Gravity * Runner.DeltaTime;
        }
        
        if(Input.GetKey(KeyCode.Space) && ch.isGrounded)
        {
            velocity.y = jumpForce;
        }
        
        float HorizontalInput = Input.GetAxis("Horizontal");
        float VerticalInput = Input.GetAxis("Vertical");
        Vector3 movement = new Vector3(HorizontalInput, 0, VerticalInput) * playerSpeed * Runner.DeltaTime;
        
        ch.Move(movement + velocity * Runner.DeltaTime);
        
        if (movement != Vector3.zero)
        {
            transform.forward = movement;
        }
    }
}