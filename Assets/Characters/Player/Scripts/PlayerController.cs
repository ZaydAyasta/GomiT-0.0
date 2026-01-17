using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // Debug
    private float debugTimer;
    public float debugInterval = 0.2f;

    public PlayerData data;
    public PlayerInputHandler input;
    public Transform groundCheck;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private Animator animator;

    [Header("Ground Check")]
    public Vector2 groundCheckSize = new Vector2(0.6f, 0.15f);
    private bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        CheckGround();

        float speed = Mathf.Abs(input.Move.x);
        GetComponent<PlayerAnimation>()?.SetSpeed(speed);

        float yVel = rb.linearVelocity.y;

        animator.SetBool("isGrounded", isGrounded);
        animator.SetFloat("yVelocity", yVel);

        // Aquí se controla el intervalo del Debug.Log
        //debugTimer += Time.deltaTime;
        //if (debugTimer >= debugInterval)
        //{
        //    Debug.Log(
        //        $"[PlayerController] yVelocity: {yVel:F3} | isGrounded: {isGrounded}"
        //    );
        //    debugTimer = 0f;
        //}

        if (input.JumpPressed && isGrounded)
        {
            Jump();
        }
    }

    private void FixedUpdate()
    {
        float targetSpeed = input.Move.x *
            (input.RunHeld ? data.runSpeed : data.walkSpeed);

        rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * data.jumpForce, ForceMode2D.Impulse);

        animator.SetTrigger("Jump");
    }

    private void CheckGround()
    {
        isGrounded = Physics2D.OverlapBox(
            groundCheck.position,
            groundCheckSize,
            0f,
            groundLayer
        );
    }


private void OnDrawGizmosSelected()
{
    if (groundCheck == null) return;

    Gizmos.color = Color.red;
    Gizmos.DrawWireCube(
        groundCheck.position,
        groundCheckSize
    );
}


}
