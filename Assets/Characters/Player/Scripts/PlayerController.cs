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
    private SpriteRenderer spriteRenderer;

    [Header("Ground Check")]
    public Vector2 groundCheckSize = new Vector2(0.6f, 0.15f);
    private bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        CheckGround();

        float horizontalInput = input.Move.x;
        float speed = Mathf.Abs(horizontalInput);
        float yVel = rb.linearVelocity.y;

        animator.SetBool("isGrounded", isGrounded);
        animator.SetFloat("yVelocity", yVel);

        bool isInAir = !isGrounded || Mathf.Abs(yVel) > 0.1f;
        animator.SetBool("isInAir", isInAir);

        GetComponent<PlayerAnimation>()?.SetSpeed(speed, isGrounded);

        if (spriteRenderer != null)
        {
            if (horizontalInput > 0.01f) spriteRenderer.flipX = false;
            else if (horizontalInput < -0.01f) spriteRenderer.flipX = true;
        }

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
