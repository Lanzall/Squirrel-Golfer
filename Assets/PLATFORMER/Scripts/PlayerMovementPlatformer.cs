using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovementPlatformer : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpHeight = 1f;

    //InputSystem
    private PlatformerPlayer controls;
    private InputAction moveAction;
    private InputAction jumpAction;

    private void Awake()
    {
        controls = new PlatformerPlayer();
        moveAction = controls.Player.Move;
        jumpAction = controls.Player.Jump;
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }
}
