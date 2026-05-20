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
    private readonly int holdSpeedHash = Animator.StringToHash("HoldSpeed");

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

    [Header("Grapping / Holding")]
    public GrappableHighlighter highlighter;
    private enum HoldState { Normal, Holding }
    private HoldState holdState = HoldState.Normal;
    private GrappableObject currentAnchor;
    private Transform lastArmGrip = null;
    private Vector2 holdOffset;
    private Vector2 holdStartOffset;

    [Header("Holding Movement Settings")]
    public float maxHoldDistance = 1.5f;
    public float holdMoveSpeed = 2.0f;
    public bool holdVerticalLock = true;
    public bool holdUsePhysics = true;

    private RigidbodyType2D prevBodyType;
    private float prevGravityScale;
    private bool isCrouchingOnEnter = false;
    private float movementLockedUntil;

    [Header("Arm Renderer")]
    public GomiArmRenderer armRenderer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void DebugAnimatorState()
    {
        if (animator == null) return;
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

        bool isCrouching;
        if (holdState == HoldState.Holding)
        {
            isCrouching = input.CrouchHeld;

            if (armRenderer != null && currentAnchor != null)
            {
                Transform gripNow = input.CrouchHeld ? currentAnchor.crouchGrip : currentAnchor.standingGrip;
                if (gripNow != lastArmGrip)
                {
                    lastArmGrip = gripNow;
                    armRenderer.SetTargetTransform(gripNow);
                }
            }
        }
        else
        {
            isCrouching = input.CrouchHeld && isGrounded;
        }

        animator.SetBool(isCrouchingHash, isCrouching);

        if (input.GrabPressed)
            TryEnterHolding();

        if (holdState == HoldState.Holding && !input.GrabHeld)
            ReleaseHoldingWithSlingshot();

        if (holdState == HoldState.Holding)
        {
            animator.SetBool("isHolding", true);
            animator.SetFloat(holdSpeedHash, Mathf.Abs(horizontalInput));

            float dx = horizontalInput * holdMoveSpeed * Time.deltaTime;
            holdOffset.x += dx;
            holdOffset.x = Mathf.Clamp(
                holdOffset.x,
                holdStartOffset.x - maxHoldDistance,
                holdStartOffset.x + maxHoldDistance
            );

            if (spriteRenderer != null)
            {
                if (holdOffset.x > 0.01f) spriteRenderer.flipX = false;
                else if (holdOffset.x < -0.01f) spriteRenderer.flipX = true;
            }

            if (input.JumpPressed)
            {
                JumpFromHold();
            }

            return;
        }

        GetComponent<PlayerAnimation>()?.SetSpeed(speed, isGrounded);
        animator.SetBool("isHolding", false);
        animator.SetFloat(holdSpeedHash, 0f);

        if (spriteRenderer != null)
        {
            if (horizontalInput > 0.01f) spriteRenderer.flipX = false;
            else if (horizontalInput < -0.01f) spriteRenderer.flipX = true;
        }

        if (input.JumpPressed && isGrounded)
            Jump();
    }

    private void FixedUpdate()
    {
        if (holdState == HoldState.Holding)
        {
            if (holdUsePhysics && currentAnchor != null)
            {
                Vector2 gripPos = currentAnchor.GetGripPosition(isCrouchingOnEnter);

                float targetX = gripPos.x + holdOffset.x;
                float deltaX = targetX - rb.position.x;

                float desiredVelX = Mathf.Clamp(deltaX / Time.fixedDeltaTime, -holdMoveSpeed, holdMoveSpeed);

                rb.linearVelocity = new Vector2(desiredVelX, rb.linearVelocity.y);

                return;
            }

            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        if (Time.time < movementLockedUntil)
            return;

        float baseSpeed = input.Move.x * (input.RunHeld ? data.runSpeed : data.walkSpeed);

        bool isCrouchingFixed = input.CrouchHeld && isGrounded;
        float crouchMultiplier = isCrouchingFixed ? 0.4f : 1f;

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

    private void TryEnterHolding()
    {
        if (highlighter == null) return;

        GrappableObject g = highlighter.GetHighlighted();
        if (g == null) return;

        EnterHolding(g);
    }

    private void EnterHolding(GrappableObject anchor)
    {
        currentAnchor = anchor;
        holdState = HoldState.Holding;

        isCrouchingOnEnter = input.CrouchHeld;

        if (armRenderer != null)
        {
            Transform grip = isCrouchingOnEnter
                ? anchor.crouchGrip
                : anchor.standingGrip;

            lastArmGrip = grip;
            armRenderer.SetTargetTransform(grip);
        }

        Vector2 gripPos = currentAnchor.GetGripPosition(isCrouchingOnEnter);
        holdOffset = (Vector2)transform.position - gripPos;
        holdStartOffset = holdOffset;

        prevBodyType = rb.bodyType;
        prevGravityScale = rb.gravityScale;

        if (holdUsePhysics)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.angularVelocity = 0f;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            if (currentAnchor != null)
            {
                Vector2 grip = currentAnchor.GetGripPosition(isCrouchingOnEnter);
                Vector2 targetPos = grip + holdOffset;
                if (holdVerticalLock)
                    targetPos.y = grip.y + holdOffset.y;
                transform.position = targetPos;
            }
        }

        animator.SetBool("isHolding", true);
    }

    private void ExitHolding()
    {
        rb.bodyType = prevBodyType;
        rb.gravityScale = prevGravityScale;

        animator.SetBool("isHolding", false);

        currentAnchor = null;
        holdState = HoldState.Normal;

        if (armRenderer != null)
        {
            armRenderer.SetTargetTransform(null);
            lastArmGrip = null;
        }
    }

    private void ReleaseHoldingWithSlingshot()
    {
        Vector2 launchVelocity = CalculateSlingshotLaunchVelocity();

        ExitHolding();

        if (launchVelocity == Vector2.zero)
            return;

        if (rb.bodyType != RigidbodyType2D.Dynamic)
            rb.bodyType = RigidbodyType2D.Dynamic;

        movementLockedUntil = Time.time + data.slingshotControlLockTime;
        rb.linearVelocity = launchVelocity;
    }

    private Vector2 CalculateSlingshotLaunchVelocity()
    {
        if (data == null || currentAnchor == null)
            return Vector2.zero;

        Vector2 gripPos = currentAnchor.GetGripPosition(isCrouchingOnEnter);
        Vector2 currentOffset = (Vector2)transform.position - gripPos;
        Vector2 pullVector = currentOffset - holdStartOffset;

        float stretch = pullVector.magnitude;
        if (stretch < data.minSlingshotStretch)
            return Vector2.zero;

        float minSpeed = Mathf.Min(data.minSlingshotLaunchSpeed, data.maxSlingshotLaunchSpeed);
        float maxSpeed = Mathf.Max(data.minSlingshotLaunchSpeed, data.maxSlingshotLaunchSpeed);
        float speed = Mathf.Clamp(
            stretch * Mathf.Max(0f, data.slingshotVelocityPerUnit),
            minSpeed,
            maxSpeed
        );

        return -pullVector.normalized * speed;
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * data.jumpForce, ForceMode2D.Impulse);
        animator.SetTrigger("Jump");
    }

    private void JumpFromHold()
    {
        ExitHolding();

        if (rb.bodyType != RigidbodyType2D.Dynamic)
            rb.bodyType = RigidbodyType2D.Dynamic;

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
        if (wallCheck == null)
        {
            isTouchingWall = false;
            return;
        }

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

        if (currentAnchor != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 gpos = currentAnchor.transform.position;
            Gizmos.DrawWireSphere(gpos, maxHoldDistance);
        }
    }
}
