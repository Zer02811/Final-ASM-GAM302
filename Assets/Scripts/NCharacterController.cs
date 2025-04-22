using Fusion;
using UnityEngine;

public class NCharacterController : NetworkBehaviour
{

    public Vector3 moveDirection;
    public float moveSpeed = 3f; 

    public Animator animator; 
    private void Update()
    {
        moveDirection.x = Input.GetAxis("Horizontal"); 
        moveDirection.z = Input.GetAxis("Vertical");   
    }
    public override void FixedUpdateNetwork()
    {
        transform.position += moveDirection * moveSpeed * Runner.DeltaTime; 

        if (moveDirection.magnitude > 0)
        {
             transform.forward = moveDirection.normalized; 
        }
        animator.SetFloat("speed", moveDirection.magnitude * moveSpeed); 
    }
}