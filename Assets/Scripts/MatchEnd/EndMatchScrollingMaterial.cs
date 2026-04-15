using UnityEngine;

public class EndMatchScrollingMaterial : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private int materialIndex = 0;
    [SerializeField] private string texturePropertyName = "_MainTex";

    [Header("Scroll")]
    [SerializeField] private bool scrollOnStart = true;
    [SerializeField] private Vector2 scrollSpeed = new Vector2(0f, -0.25f);
    [SerializeField] private Vector2 startOffset = Vector2.zero;
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Material runtimeMaterial;
    private Vector2 currentOffset;
    private int propertyId;
    private bool isScrolling;

    private void Awake()
    {
        propertyId = Shader.PropertyToID(texturePropertyName);
        ResolveRuntimeMaterial();
        currentOffset = startOffset;
        ApplyOffset(currentOffset);
        isScrolling = scrollOnStart;
    }

    private void Update()
    {
        if (!isScrolling) return;
        if (runtimeMaterial == null) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f) return;

        currentOffset += scrollSpeed * dt;
        currentOffset.x = Mathf.Repeat(currentOffset.x, 1f);
        currentOffset.y = Mathf.Repeat(currentOffset.y, 1f);

        ApplyOffset(currentOffset);
    }

    public void SetScrolling(bool enabled)
    {
        isScrolling = enabled;
    }

    public void ResetOffset()
    {
        currentOffset = startOffset;
        ApplyOffset(currentOffset);
    }

    public Vector2 GetCurrentOffset()
    {
        return currentOffset;
    }

    private void ResolveRuntimeMaterial()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        if (targetRenderer == null)
        {
            if (debugLogs)
                Debug.LogWarning("[EndMatchScrollingMaterial] No renderer assigned.");
            return;
        }

        Material[] mats = targetRenderer.materials;
        if (mats == null || mats.Length == 0)
            return;

        int index = Mathf.Clamp(materialIndex, 0, mats.Length - 1);
        runtimeMaterial = mats[index];

        if (debugLogs)
            Debug.Log($"[EndMatchScrollingMaterial] Using material index {index} on {targetRenderer.name}");
    }

    private void ApplyOffset(Vector2 offset)
    {
        if (runtimeMaterial == null)
            return;

        runtimeMaterial.SetTextureOffset(propertyId, offset);
    }
}