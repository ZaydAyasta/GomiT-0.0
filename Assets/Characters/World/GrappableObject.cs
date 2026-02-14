using UnityEngine;

public class GrappableObject : MonoBehaviour
{
    public Transform standingGrip;
    public Transform crouchGrip;

    public float maxStretchDistance = 5f;

    public Vector2 GetGripPosition(bool isCrouching)
    {
        if (isCrouching && crouchGrip != null)
            return crouchGrip.position;

        if (!isCrouching && standingGrip != null)
            return standingGrip.position;

        return transform.position;
    }
}
