using Fusion;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    private CharacterController _controller;
    private Animator _animator;

    [Header("References")]
    public Transform GraphicsRoot;
    public Transform CameraPivot;

    [Header("Movement Settings")]
    public float PlayerSpeed = 2f;
    public float JumpForce = 5f;
    public float Gravity = -9.81f;

    // Networked properties host to clients
    [Networked] private Vector3 _velocity { get; set; }
    [Networked] private NetworkBool _isGrounded { get; set; }
    
    public Camera Camera;

    private void Awake(){
        _controller = GetComponent<CharacterController>();
        RefreshAnimatorReference();
    }

    public void RefreshAnimatorReference(){
        if (GraphicsRoot != null)
            _animator = GraphicsRoot.GetComponentInChildren<Animator>(false);
        else
            _animator = GetComponentInChildren<Animator>(false);
    }

  public override void FixedUpdateNetwork(){
      if (GetInput(out PlayerInputData data)){
      _isGrounded = _controller.isGrounded;
      // Local variable to modify velocity
      Vector3 currentVelocity = _velocity;
      if (_isGrounded && currentVelocity.y < 0){
          currentVelocity.y = -2f;
       }
      //Handle Rotation
       Vector3 camEuler = data.CameraRotation.eulerAngles;
      transform.rotation = Quaternion.Euler(0, camEuler.y, 0);
      //Handle Movement
      Vector3 move = transform.rotation * new Vector3(data.MoveDirection.x, 0, data.MoveDirection.z) * PlayerSpeed;
          _controller.Move(move * Runner.DeltaTime);
      //Handle Jump
      if (data.JumpPressed && _isGrounded){
        currentVelocity.y = JumpForce;
          if (HasStateAuthority) RPC_TriggerJump();
      }
      //Apply Gravity to the local variable
      currentVelocity.y += Gravity * Runner.DeltaTime;
      //RE-ASSIGN the modified velocity back to the Networked property
      _velocity = currentVelocity;
      //Move the controller using the updated velocity
      _controller.Move(_velocity * Runner.DeltaTime);
      UpdateAnimations(move);
      }
  }

    private void UpdateAnimations(Vector3 move){
        if (_animator == null) return;
        // Convert world movement to local for Animator (MoveX, MoveZ)
        Vector3 localMove = transform.InverseTransformDirection(move.normalized);
        _animator.SetFloat("MoveX", localMove.x);
        _animator.SetFloat("MoveZ", localMove.z);
        _animator.SetBool("IsGrounded", _isGrounded);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TriggerJump(){
      if (_animator != null) _animator.SetTrigger("Jump");
    }

    public override void Spawned(){
    // HasInputAuthority is true for the player who controls this specific prefab
        if (HasInputAuthority){
          Camera = Camera.main;
          Transform targetTransform = CameraPivot != null ? CameraPivot : transform;
            
            // Setup local camera follow
            var fpCam = Camera.GetComponent<FirstPersonCamera>();
            if(fpCam != null){
                fpCam.SetTarget(targetTransform, GraphicsRoot != null ? GraphicsRoot.gameObject : gameObject);
            }
        }
    }
}