using UnityEngine;

public class GrappableHighlighter : MonoBehaviour
{
    [Header("Rango de búsqueda")]
    public float grabRange = 3f;
    [Tooltip("Si usas layers para filtrar, pon aquí la capa de objetos agarrables (opcional).")]
    public LayerMask grappableLayer = ~0;

    [Header("Opciones de sobrescritura")]
    public bool overrideValues = false;
    public Color overrideColor = Color.red;
    public float overrideThickness = 0.000005f;

    private GrappableObject highlighted;

    void Update()
    {
        UpdateHighlight();
    }

    void UpdateHighlight()
    {
        GrappableObject closest = null;
        float minDist = grabRange;

        foreach (var g in GrappableObject.All)
        {
            if (g == null) continue;

            float d = Vector2.Distance(transform.position, g.transform.position);
            if (d <= minDist)
            {
                minDist = d;
                closest = g;
            }
        }

        if (closest != highlighted)
        {
            if (highlighted != null) highlighted.SetHighlight(false);
            highlighted = closest;
            if (highlighted != null)
            {
                if (overrideValues)
                    highlighted.SetHighlight(true, overrideColor, overrideThickness);
                else
                    highlighted.SetHighlight(true);
            }
        }
    }

    public GrappableObject GetHighlighted() => highlighted;
}
