using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Rigidbody rb;

    private Vector2 moveDirection;
    public float moveSpeed = 5f;

    private void Awake()
    {
        rb = this.GetComponent<Rigidbody>();
    }

    void Start()
    {
        
    }

    void Update()
    {
        rb.linearVelocity = new Vector3(moveDirection.x * moveSpeed, rb.linearVelocity.y, moveDirection.y * moveSpeed);
    }

    public void OnMove(InputAction.CallbackContext Move)
    {
        moveDirection = Move.ReadValue<Vector2>();
    }
}
