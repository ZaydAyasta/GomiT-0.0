using UnityEngine;

[ExecuteAlways]
public class GomiArmRenderer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform del hombro (inicio del brazo).")]
    public Transform shoulder;

    [Tooltip("Transform objetivo (puede ser anchor.transform o un Vector3 world pos).")]
    public Transform targetTransform;
    public Vector2 targetPositionOverride;
    [Tooltip("Si true se usa targetTransform; si false se toma targetPositionOverride.")]
    public bool useTargetTransform = true;

    [Header("Sprite Renderers (children)")]
    public SpriteRenderer capStart;
    public SpriteRenderer capMid;
    public SpriteRenderer capEnd;

    [Header("Tuning")]
    [Tooltip("Longitud en unidades (world units) que ocupa cada cap. Si 0, se calcula desde sprite bounds (si existe).")]
    public float capLength = 0.2f;

    [Tooltip("Grosor (world units) del brazo (y size de la pieza middle).")]
    public float thickness = 0.25f;

    [Tooltip("Interpolación para suavizar rotación/posicion (0 snap, >0 smoothing).")]
    [Range(0f, 20f)]
    public float smoothSpeed = 12f;

    [Tooltip("Si true, el middle se renderiza como Tiled y su size se actualiza vía SpriteRenderer.size.")]
    public bool useTiledMiddle = true;

    [Header("Optional")]
    [Tooltip("Si true, se escala caps para seguir 'thickness' (puede romper diseño si sprite no pensado para eso).")]
    public bool scaleCapsWithThickness = true;

    Vector3 prevMidScale = Vector3.one;
    Vector3 velocityPos;
    float velocityRot;

    private void Reset()
    {
        if (capStart == null) capStart = transform.Find("cap_start")?.GetComponent<SpriteRenderer>();
        if (capMid == null) capMid = transform.Find("cap_mid")?.GetComponent<SpriteRenderer>();
        if (capEnd == null) capEnd = transform.Find("cap_end")?.GetComponent<SpriteRenderer>();
    }

    private void OnValidate()
    {
        if (capMid != null)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (capMid != null)
                {
                    if (useTiledMiddle)
                        capMid.drawMode = SpriteDrawMode.Tiled;
                    else
                        capMid.drawMode = SpriteDrawMode.Simple;
                }
            };
#endif
        }
    }

    private void LateUpdate()
    {

        if (shoulder == null) return;

        Vector3 targetWorld;

        if (useTargetTransform && targetTransform != null)
        {
            targetWorld = targetTransform.position;
        }
        else
        {
            targetWorld = shoulder.position;
        }

        UpdateArmVisual(shoulder.position, targetWorld, Time.deltaTime);
    }

    void UpdateArmVisual(Vector3 startWorld, Vector3 endWorld, float dt)
    {
        Vector3 dir = endWorld - startWorld;
        float totalLength = dir.magnitude;
        if (totalLength < 0.0001f)
        {
            // zero-length: hide middle and overlap caps
            if (capMid != null) capMid.enabled = false;
            if (capStart != null) capStart.transform.position = startWorld;
            if (capEnd != null) capEnd.transform.position = startWorld;
            return;
        }

        Vector3 dirNorm = dir.normalized;

        float capLen = capLength;
        if (capLen <= 0f)
        {
            if (capStart != null && capStart.sprite != null)
            {
                capLen = capStart.sprite.bounds.size.y * capStart.transform.lossyScale.y;
            }
            else if (capEnd != null && capEnd.sprite != null)
            {
                capLen = capEnd.sprite.bounds.size.y * capEnd.transform.lossyScale.y;
            }
            else
            {
                capLen = 0.1f;
            }
        }

        float usable = Mathf.Max(0f, totalLength - 2f * capLen);
        if (usable <= 0f)
        {
            Vector3 startCapPos = startWorld;
            Vector3 endCapPos = endWorld;

            SetCapTransform(capStart, startCapPos, dirNorm, thickness);
            SetCapTransform(capEnd, endCapPos, dirNorm, thickness);

            if (capMid != null) capMid.enabled = false;
            return;
        }

        Vector3 startCapWorld = startWorld + dirNorm * (0f); // capStart pivot at shoulder
        Vector3 endCapWorld = startWorld + dirNorm * totalLength; // end at anchor

        Vector3 midCenter = startWorld + dirNorm * (capLen + usable * 0.5f);

        float angle = Mathf.Atan2(dirNorm.y, dirNorm.x) * Mathf.Rad2Deg - 90f; // sprite up = Y
        if (smoothSpeed > 0f)
        {
            Vector3 curPos = transform.position;
            Vector3 targetPos = transform.position;
        }

        SetCapTransform(capStart, startWorld, dirNorm, thickness);
        SetCapTransform(capEnd, endCapWorld, dirNorm, thickness);

        if (capMid != null)
        {
            capMid.enabled = true;

            capMid.transform.position = midCenter;
            capMid.transform.up = dirNorm;

            if (useTiledMiddle)
            {
                capMid.drawMode = SpriteDrawMode.Tiled;

                Vector2 newSize = new Vector2(thickness, usable);
                capMid.size = newSize;
            }
            else
            {
                //fallback
                Vector3 localScale = capMid.transform.localScale;
                float spriteLen = 1f;
                if (capMid.sprite != null)
                    spriteLen = capMid.sprite.bounds.size.y;

                float scaleY = usable / Mathf.Max(0.0001f, spriteLen);
                capMid.transform.localScale = new Vector3(thickness / Mathf.Max(0.0001f, capMid.sprite.bounds.size.x), scaleY, 1f);
            }
        }
    }

    void SetCapTransform(SpriteRenderer cap, Vector3 worldPos, Vector3 dirNorm, float thickness)
    {
        if (cap == null) return;

        // Assumes cap sprite pivot is at the base (bottom) or centered depending on art; allow minor offset via inspector later.
        cap.transform.position = worldPos;
        cap.transform.up = dirNorm;

        if (scaleCapsWithThickness)
        {
            // scale uniformly in local X to match thickness roughly
            if (cap.sprite != null)
            {
                float spriteWidth = cap.sprite.bounds.size.x;
                if (spriteWidth > 0f)
                {
                    float scaleX = thickness / spriteWidth;
                    Vector3 s = cap.transform.localScale;
                    cap.transform.localScale = new Vector3(scaleX, s.y, s.z);
                }
            }
        }
    }

    public void SetTargetPosition(Vector2 worldPos)
    {
        useTargetTransform = false;
        targetPositionOverride = worldPos;
    }

    public void SetTargetTransform(Transform t)
    {
        useTargetTransform = true;
        targetTransform = t;
    }
}