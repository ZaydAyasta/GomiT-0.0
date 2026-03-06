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
    private Transform lastArmGrip = null;
    private Vector2 holdOffset;

    [Header("Holding Movement Settings")]
    [Tooltip("Máxima distancia horizontal desde anchor")]
    public float maxHoldDistance = 1.5f;
    [Tooltip("Velocidad durante el anchor (m/s)")]
    public float holdMoveSpeed = 2.0f;
    [Tooltip("Si true, posición vertical bloqueada respecto al grip.")]
    public bool holdVerticalLock = true;

    [Tooltip("Si true, usa física durante el hold (gravedad activa). Si false, cuerpo kinematic y se posiciona manualmente.")]
    public bool holdUsePhysics = true;

    private RigidbodyType2D prevBodyType;
    private float prevGravityScale;
    private bool isCrouchingOnEnter = false;

    [Header("Arm Renderer")]
    public GomiArmRenderer armRenderer;

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
        if (holdState == HoldState.Holding){
            isCrouching = input.CrouchHeld;

            if (armRenderer != null && currentAnchor != null)
            {
                Transform gripNow = input.CrouchHeld ? currentAnchor.crouchGrip : currentAnchor.standingGrip;
                if (gripNow != lastArmGrip)
                {
                    lastArmGrip = gripNow;
                    armRenderer.SetTargetTransform(gripNow);
                    Debug.Log($"[PlayerController] Updated arm target while holding -> {(gripNow != null ? gripNow.name : "null")}, crouchHeld={input.CrouchHeld}");
                }
            }
        }else
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

            float dx = horizontalInput * holdMoveSpeed * Time.deltaTime;
            holdOffset.x += dx;
            holdOffset.x = Mathf.Clamp(holdOffset.x, -maxHoldDistance, maxHoldDistance);

            if (spriteRenderer != null)
            {
                if (holdOffset.x > 0.01f) spriteRenderer.flipX = false;
                else if (holdOffset.x < -0.01f) spriteRenderer.flipX = true;
            }

            // Permitir saltar desde hold: si se pulsa, salir del hold y aplicar salto.
            if (input.JumpPressed)
            {
                JumpFromHold();
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

        // Salto normal solo si en suelo
        if (input.JumpPressed && isGrounded)
            Jump();
    }

    private void FixedUpdate()
    {
        if (holdState == HoldState.Holding)
        {
            // MODO A: usar física durante hold (recomendado para mantener gravedad)
            if (holdUsePhysics && currentAnchor != null)
            {
                Vector2 gripPos = currentAnchor.GetGripPosition(isCrouchingOnEnter);

                float targetX = gripPos.x + holdOffset.x;
                float deltaX = targetX - rb.position.x;

                float desiredVelX = Mathf.Clamp(deltaX / Time.fixedDeltaTime, -holdMoveSpeed, holdMoveSpeed);

                rb.linearVelocity = new Vector2(desiredVelX, rb.linearVelocity.y);

                return;
            }

            // MODO B: comportamiento legacy (kinematic + posicionamiento directo)
            // Esto mantiene la lógica original: congelas Y y colocas transform directamente.
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

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

        isCrouchingOnEnter = input.CrouchHeld;

        if (armRenderer != null)
        {
            Transform grip = isCrouchingOnEnter
                ? anchor.crouchGrip
                : anchor.standingGrip;

            lastArmGrip = grip;
            Debug.Log($"[PlayerController] EnterHolding -> arm target: {(grip != null ? grip.name : "null")}, crouching={isCrouchingOnEnter}");
            armRenderer.SetTargetTransform(grip);
        }

        Vector2 gripPos = currentAnchor.GetGripPosition(isCrouchingOnEnter);
        holdOffset = (Vector2)transform.position - gripPos;
        holdOffset.x = Mathf.Clamp(holdOffset.x, -maxHoldDistance, maxHoldDistance);

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
            // opción A: quitar target para que deje de usar transform
            armRenderer.SetTargetTransform(null);
            lastArmGrip = null;

            // opción B (si quieres que el brazo se recoja hacia el hombro):
            // armRenderer.SetTargetPosition(armRenderer.shoulder != null ? (Vector2)armRenderer.shoulder.position : (Vector2)transform.position);
        }
    }

    // Salto normal
    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * data.jumpForce, ForceMode2D.Impulse);
        animator.SetTrigger("Jump");
    }

    // Salto cuando vienes de Hold: salir del hold y ejecutar salto limpio.
    private void JumpFromHold()
    {
        // Salimos primero del modo hold (restaurando bodyType y gravedad)
        ExitHolding();

        // Asegurarnos de que el rigidbody está en modo dinámico para que AddForce funcione
        if (rb.bodyType != RigidbodyType2D.Dynamic)
            rb.bodyType = RigidbodyType2D.Dynamic;

        // Resetear vertical para aplicar impulso consistente
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        // Aplicar impulso
        rb.AddForce(Vector2.up * data.jumpForce, ForceMode2D.Impulse);

        // Trigger de animación de salto
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
            
            //no se si esto funcionará xd
            //gomiArmRenderer.SetTargetTransform(currentAnchor.transform);
        }
    }
}