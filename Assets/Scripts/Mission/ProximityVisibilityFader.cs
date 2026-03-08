using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fades renderers based on distance to camera (or a custom target).
/// Useful for showing helper areas only when players are near.
/// </summary>
public class ProximityVisibilityFader : MonoBehaviour
{
    [Header("Distance")]
    [SerializeField] private float startAppearDistance = 12f;
    [SerializeField] private float fullyVisibleDistance = 6f;
    [SerializeField] private float fadeSpeed = 4f;

    [Header("Target")]
    [SerializeField] private Transform distanceTarget;
    [SerializeField] private bool fallbackToMainCamera = true;

    [Header("Renderers")]
    [SerializeField] private bool autoCollectChildRenderers = true;
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private bool disableRendererWhenInvisible = true;

    private readonly List<Material> runtimeMaterials = new List<Material>();
    private float currentAlpha;

    private void Awake()
    {
        CollectRenderersIfNeeded();
        CacheRuntimeMaterials();
        PrepareMaterialsForFade();

        currentAlpha = 0f;
        ApplyAlpha(0f);
    }

    private void LateUpdate()
    {
        Transform target = ResolveTarget();
        if (target == null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        float targetAlpha = EvaluateTargetAlpha(distance);

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Mathf.Max(0.01f, fadeSpeed) * Time.deltaTime);
        ApplyAlpha(currentAlpha);
    }

    private Transform ResolveTarget()
    {
        if (distanceTarget != null)
        {
            return distanceTarget;
        }

        if (fallbackToMainCamera && Camera.main != null)
        {
            return Camera.main.transform;
        }

        return null;
    }

    private float EvaluateTargetAlpha(float distance)
    {
        float start = Mathf.Max(0f, startAppearDistance);
        float full = Mathf.Clamp(fullyVisibleDistance, 0f, start);

        if (distance >= start)
        {
            return 0f;
        }

        if (distance <= full)
        {
            return 1f;
        }

        float t = Mathf.InverseLerp(start, full, distance);
        return Mathf.Clamp01(t);
    }

    private void CollectRenderersIfNeeded()
    {
        if (!autoCollectChildRenderers) return;

        targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void CacheRuntimeMaterials()
    {
        runtimeMaterials.Clear();
        if (targetRenderers == null) return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer r = targetRenderers[i];
            if (r == null) continue;

            Material[] mats = r.materials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null) continue;
                runtimeMaterials.Add(mats[m]);
            }
        }
    }

    private void PrepareMaterialsForFade()
    {
        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            Material mat = runtimeMaterials[i];
            if (mat == null) continue;

            // Standard pipeline setup.
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3f);
            }

            // URP Lit setup.
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
            }

            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;

            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0f);
            }
        }
    }

    private void ApplyAlpha(float alpha)
    {
        bool visible = alpha > 0.01f;

        if (targetRenderers != null)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer r = targetRenderers[i];
                if (r == null) continue;

                if (disableRendererWhenInvisible)
                {
                    r.enabled = visible;
                }
            }
        }

        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            Material mat = runtimeMaterials[i];
            if (mat == null) continue;

            if (mat.HasProperty("_BaseColor"))
            {
                Color c = mat.GetColor("_BaseColor");
                c.a = alpha;
                mat.SetColor("_BaseColor", c);
            }

            if (mat.HasProperty("_Color"))
            {
                Color c = mat.GetColor("_Color");
                c.a = alpha;
                mat.SetColor("_Color", c);
            }
        }
    }

    private void OnValidate()
    {
        startAppearDistance = Mathf.Max(0f, startAppearDistance);
        fullyVisibleDistance = Mathf.Clamp(fullyVisibleDistance, 0f, startAppearDistance);
        fadeSpeed = Mathf.Max(0.01f, fadeSpeed);
    }
}
