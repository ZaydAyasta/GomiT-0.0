using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 Move { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool RunHeld { get; private set; }

    public bool GrabPressed { get; private set; }
    public bool GrabHeld { get; private set; }

    public bool CrouchHeld { get; private set; }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Move = ctx.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            JumpPressed = true;
        }
    }

    public void OnRun(InputAction.CallbackContext ctx)
    {
        RunHeld = ctx.ReadValueAsButton();
    }

    public void OnGrab(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            GrabPressed = true;

        GrabHeld = ctx.ReadValueAsButton();
    }

    public void OnCrouch(InputAction.CallbackContext ctx)
    {
        CrouchHeld = ctx.ReadValueAsButton();
    }

    private void LateUpdate()
    {
        JumpPressed = false;
        GrabPressed = false;
    }
}
