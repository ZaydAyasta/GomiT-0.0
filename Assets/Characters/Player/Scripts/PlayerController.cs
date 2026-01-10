using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public PlayerData data;
    public PlayerInputHandler input;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        float speed = Mathf.Abs(input.Move.x);
        GetComponent<PlayerAnimation>()?.SetSpeed(speed);
    }

    private void FixedUpdate()
    {
        float targetSpeed = input.Move.x *
            (input.RunHeld ? data.runSpeed : data.walkSpeed);

        rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);

        if (input.JumpPressed)
            rb.AddForce(Vector2.up * data.jumpForce, ForceMode2D.Impulse);
    }
}