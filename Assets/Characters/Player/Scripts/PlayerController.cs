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

    [Header("Grapping / Holding")]
    public GrappableHighlighter highlighter;
    private enum HoldState { Normal, Holding }
    private HoldState holdState = HoldState.Normal;
    private GrappableObject currentAnchor;
    private Vector2 holdOffset;

    [Header("Holding Movement Settings")]
    [Tooltip("Máxima distancia horizontal desde anchor")]
    public float maxHoldDistance = 1.5f;
    [Tooltip("Velocidad horizontal durante el anchor")]
    public float holdMoveSpeed = 2.0f;
    [Tooltip("Si true, la posición vertical se bloquea respecto al grip (no aplica gravedad)")]
    public bool holdVerticalLock = false;

    [Header("Holding Vertical (Y) Settings")]
    [Tooltip("Máxima distancia hacia arriba desde el anchor")]
    public float maxHoldUpDistance = 1.0f;
    [Tooltip("Máxima distancia hacia abajo desde el anchor")]
    public float maxHoldDownDistance = 2.0f;
    [Tooltip("Gravedad aplicada mientras está en hold (sensación de peso)")]
    public float holdGravity = 9.8f;
    [Tooltip("Altura mínima sobre la colisión detectada para evitar traspasar el suelo")]
    public float groundClearance = 0.05f;
    [Tooltip("Máxima distancia de raycast hacia abajo para detectar suelo y corregir posición")]
    public float groundRaycastDistance = 5f;

    private float holdVerticalVelocity = 0f;

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

        bool isCrouching;
        if (holdState == HoldState.Holding)
            isCrouching = input.CrouchHeld;
        else
            isCrouching = input.CrouchHeld && isGrounded;

        animator.SetBool(isCrouchingHash, isCrouching);

        if (input.GrabPressed)
            TryEnterHolding();

        if (holdState == HoldState.Holding && !input.GrabHeld)
            ExitHolding();

        if (holdState == HoldState.Holding)
        {
            GetComponent<PlayerAnimation>()?.SetSpeed(0f, false);
            animator.SetBool("isHolding", true);

            if (currentAnchor != null)
            {
                float dx = horizontalInput * holdMoveSpeed * Time.deltaTime;
                holdOffset.x += dx;
                holdOffset.x = Mathf.Clamp(holdOffset.x, -maxHoldDistance, maxHoldDistance);

                if (!holdVerticalLock)
                {
                    holdVerticalVelocity -= holdGravity * Time.deltaTime;
                    holdOffset.y += holdVerticalVelocity * Time.deltaTime;

                    holdOffset.y = Mathf.Clamp(holdOffset.y, -maxHoldDownDistance, maxHoldUpDistance);
                }

                Vector2 gripPos = currentAnchor.GetGripPosition(isCrouching);
                Vector2 targetPos = gripPos + holdOffset;

                Vector2 rayOrigin = targetPos + Vector2.up * 0.1f;
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, groundRaycastDistance, groundLayer);

                if (hit.collider != null)
                {
                    float allowedY = hit.point.y + groundClearance;
                    if (targetPos.y < allowedY)
                    {
                        targetPos.y = allowedY;
                        holdOffset.y = targetPos.y - gripPos.y;
                        holdVerticalVelocity = 0f;
                    }
                }

                transform.position = targetPos;

                if (spriteRenderer != null)
                {
                    if (holdOffset.x > 0.01f) spriteRenderer.flipX = false;
                    else if (holdOffset.x < -0.01f) spriteRenderer.flipX = true;
                }
            }

            return;
        }

        GetComponent<PlayerAnimation>()?.SetSpeed(speed, isGrounded);
        animator.SetBool("isHolding", false);

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
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

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

    private void TryEnterHolding()
    {
        if (highlighter == null)
        {
            Debug.LogWarning("[PlayerController] No highlighter asignado.");
            return;
        }

        GrappableObject g = highlighter.GetHighlighted();
        if (g == null)
        {
            Debug.Log("[PlayerController] No hay nada holdeable cerca.");
            return;
        }

        EnterHolding(g);
    }

    private void EnterHolding(GrappableObject anchor)
    {
        currentAnchor = anchor;
        holdState = HoldState.Holding;

        bool isCrouchingOnEnter = input.CrouchHeld;
        Vector2 gripPos = currentAnchor.GetGripPosition(isCrouchingOnEnter);
        holdOffset = (Vector2)transform.position - gripPos;

        holdOffset.x = Mathf.Clamp(holdOffset.x, -maxHoldDistance, maxHoldDistance);
        holdOffset.y = Mathf.Clamp(holdOffset.y, -maxHoldDownDistance, maxHoldUpDistance);

        holdVerticalVelocity = 0f;

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        animator.SetBool("isHolding", true);
    }

    private void ExitHolding()
    {
        rb.bodyType = RigidbodyType2D.Dynamic;

        animator.SetBool("isHolding", false);

        currentAnchor = null;
        holdState = HoldState.Normal;
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
            if (wallCheck != null) Gizmos.DrawWireCube(wallCheck.position, wallCheckSize);
        }

        if (currentAnchor != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 gpos = currentAnchor.transform.position;
            Gizmos.DrawWireSphere(gpos, maxHoldDistance);

            Gizmos.color = Color.cyan;
            Vector3 gripPos = currentAnchor.GetGripPosition(false);
            Gizmos.DrawWireSphere(gripPos + (Vector3)holdOffset, 0.05f);
        }
    }
}