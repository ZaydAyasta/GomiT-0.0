using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class GrappableObject : MonoBehaviour
{
    public static List<GrappableObject> All = new List<GrappableObject>();

    [Header("Grip Transforms (opcional)")]
    public Transform standingGrip;
    public Transform crouchGrip;

    [Header("Outline default (puedes sobrescribir en runtime)")]
    public Color outlineColor = new Color(0.44f, 0.95f, 1f, 1f); // #70F3FF
    public float outlineThickness = 1f;

    SpriteRenderer sr;
    MaterialPropertyBlock mpb;

    private void OnEnable()
    {
        All.Add(this);
        sr = GetComponent<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();

        SetHighlight(false); //off x default
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    public Vector2 GetGripPosition(bool crouching)
    {
        if (crouching && crouchGrip != null) return crouchGrip.position;
        if (!crouching && standingGrip != null) return standingGrip.position;
        return transform.position;
    }

    // activar o desactivar
    public void SetHighlight(bool active, Color? color = null, float? thickness = null)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (mpb == null) mpb = new MaterialPropertyBlock();

        sr.GetPropertyBlock(mpb);

        float t = active ? (thickness ?? outlineThickness) : 0f;
        Color c = color ?? outlineColor;

        Material mat = sr.sharedMaterial;
        if (mat != null && mat.HasProperty("_OutlineThickness") && mat.HasProperty("_OutlineColor"))
        {
            mpb.SetFloat("_OutlineThickness", t);
            mpb.SetColor("_OutlineColor", c);
            sr.SetPropertyBlock(mpb);
        }
        else
        {
            //fallback
            Debug.LogWarning($"{name}: el material no tiene _OutlineThickness/_OutlineColor. Considera exponer esas propiedades en tu Shader Graph.");
        }
    }
}
