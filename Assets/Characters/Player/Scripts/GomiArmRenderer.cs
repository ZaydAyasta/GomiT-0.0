using UnityEngine;

[ExecuteAlways]
public class GomiArmRenderer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform del hombro (inicio del brazo).")]
    public Transform shoulder;

    [Tooltip("Transform objetivo.")]
    public Transform targetTransform;

    public Vector2 targetPositionOverride;

    [Tooltip("Si true usa targetTransform.")]
    public bool useTargetTransform = true;

    [Header("Sprite Renderers (children)")]
    public SpriteRenderer capStart;
    public SpriteRenderer capMid;
    public SpriteRenderer capEnd;

    [Header("Tuning")]
    public float capLength = 0.2f;
    public float thickness = 0.25f;

    [Range(0f, 20f)]
    public float smoothSpeed = 12f;

    public bool useTiledMiddle = true;

    [Header("Optional")]
    public bool scaleCapsWithThickness = true;

    [Header("Holding switch cooldown")]
    [Tooltip("Tiempo de transición entre targets.")]
    public float switchCooldown = 0.12f;

    Vector3 prevMidScale = Vector3.one;
    Vector3 velocityPos;
    float velocityRot;

    // switching
    bool isSwitching = false;
    Vector3 switchFromWorld;
    Vector3 switchToWorld;
    float switchStartTime;

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
                    capMid.drawMode = useTiledMiddle ? SpriteDrawMode.Tiled : SpriteDrawMode.Simple;
                }
            };
#endif
        }
    }

    Vector3 GetCurrentTargetWorld()
    {
        if (useTargetTransform)
        {
            if (targetTransform != null)
                return targetTransform.position;

            return shoulder.position;
        }

        return targetPositionOverride;
    }

    private void LateUpdate()
    {
        if (shoulder == null) return;

        Vector3 currentTargetWorld;

        if (isSwitching)
        {
            float t = (Time.time - switchStartTime) / Mathf.Max(0.00001f, switchCooldown);

            if (t >= 1f)
            {
                isSwitching = false;
                currentTargetWorld = switchToWorld;
            }
            else
            {
                currentTargetWorld = Vector3.Lerp(switchFromWorld, switchToWorld, t);
            }
        }
        else
        {
            currentTargetWorld = GetCurrentTargetWorld();
        }

        UpdateArmVisual(shoulder.position, currentTargetWorld, Time.deltaTime);
    }

    void UpdateArmVisual(Vector3 startWorld, Vector3 endWorld, float dt)
    {
        Vector3 dir = endWorld - startWorld;
        float totalLength = dir.magnitude;

        if (totalLength < 0.0001f)
        {
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
                capLen = capStart.sprite.bounds.size.y * capStart.transform.lossyScale.y;
            else if (capEnd != null && capEnd.sprite != null)
                capLen = capEnd.sprite.bounds.size.y * capEnd.transform.lossyScale.y;
            else
                capLen = 0.1f;
        }

        float usable = Mathf.Max(0f, totalLength - 2f * capLen);

        if (usable <= 0f)
        {
            SetCapTransform(capStart, startWorld, dirNorm, thickness);
            SetCapTransform(capEnd, endWorld, dirNorm, thickness);

            if (capMid != null) capMid.enabled = false;

            return;
        }

        Vector3 endCapWorld = startWorld + dirNorm * totalLength;
        Vector3 midCenter = startWorld + dirNorm * (capLen + usable * 0.5f);

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
                capMid.size = new Vector2(thickness, usable);
            }
            else
            {
                float spriteLen = 1f;

                if (capMid.sprite != null)
                    spriteLen = capMid.sprite.bounds.size.y;

                float scaleY = usable / Mathf.Max(0.0001f, spriteLen);

                capMid.transform.localScale = new Vector3(
                    thickness / Mathf.Max(0.0001f, capMid.sprite.bounds.size.x),
                    scaleY,
                    1f
                );
            }
        }
    }

    void SetCapTransform(SpriteRenderer cap, Vector3 worldPos, Vector3 dirNorm, float thickness)
    {
        if (cap == null) return;

        cap.transform.position = worldPos;
        cap.transform.up = dirNorm;

        if (scaleCapsWithThickness && cap.sprite != null)
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

    public void SetTargetPosition(Vector2 worldPos)
    {
        Vector3 currentWorld = GetCurrentTargetWorld();

        switchFromWorld = currentWorld;
        switchToWorld = worldPos;
        switchStartTime = Time.time;

        isSwitching = true;

        useTargetTransform = false;
        targetPositionOverride = worldPos;
    }

    public void SetTargetTransform(Transform t)
    {
        Vector3 currentWorld = GetCurrentTargetWorld();

        Vector3 newWorld = (t != null) ? t.position : shoulder.position;

        switchFromWorld = currentWorld;
        switchToWorld = newWorld;
        switchStartTime = Time.time;

        isSwitching = true;

        useTargetTransform = true;
        targetTransform = t;
    }
}