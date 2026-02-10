using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // Debug
    private float debugTimer;
    public float debugInterval = 0.2f;

    public PlayerData data;
    public PlayerInputHandler input;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private readonly int isCrouchingHash = Animator.StringToHash("isCrouching");

    [Header("Wall Check")]
    public Transform wallCheck;
    public Vector2 wallCheckSize = new Vector2(0.1f, 1.8f);
    public LayerMask wallLayer;
    public float wallCheckOffsetX = 0.6f;

    [Header("Ground Check")]
    public Vector2 groundCheckSize = new Vector2(0.6f, 0.15f);
    public Transform groundCheck;
    public LayerMask groundLayer;
    private bool isGrounded;
    private bool isTouchingWall;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        CheckGround();
        UpdateWallCheckPosition();
        CheckWall();

        float horizontalInput = input.Move.x;
        float speed = Mathf.Abs(horizontalInput);
        float yVel = rb.linearVelocity.y;

        animator.SetBool("isGrounded", isGrounded);
        animator.SetFloat("yVelocity", yVel);

        bool isInAir = !isGrounded || Mathf.Abs(yVel) > 0.1f;
        animator.SetBool("isInAir", isInAir);

        bool isCrouching = input.CrouchHeld && isGrounded;
        animator.SetBool(isCrouchingHash, isCrouching);

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
        float baseSpeed = input.Move.x * (input.RunHeld ? data.runSpeed : data.walkSpeed);

        bool isCrouching = input.CrouchHeld && isGrounded;
        float crouchMultiplier = isCrouching ? 0.4f : 1f;

        float targetSpeed = baseSpeed * crouchMultiplier;

        if (!isGrounded && isTouchingWall && Mathf.Abs(targetSpeed) > 0.01f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);
        }
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

    private void CheckWall()
    {
        isTouchingWall = Physics2D.OverlapBox(
            wallCheck.position,
            wallCheckSize,
            0f,
            wallLayer
        );
    }

    private void UpdateWallCheckPosition()
    {
        if (spriteRenderer == null || wallCheck == null)
            return;

        float dir = spriteRenderer.flipX ? -1f : 1f;
        wallCheck.localPosition = new Vector3(
            wallCheckOffsetX * dir,
            wallCheck.localPosition.y,
            0f
        );
    }


    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }

        if (wallCheck != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(wallCheck.position, wallCheckSize);
        }
    }
}
