using UnityEngine;

/// <summary>
/// Runtime helper to detect invalid transform/bounds values that can trigger Unity's
/// "Invalid AABB aabb" warning, especially during Timeline playback.
/// </summary>
public class EndMatchAabbDiagnostics : MonoBehaviour
{
    [SerializeField] private bool scanEveryFrame = true;
    [SerializeField] private float scanIntervalSeconds = 0.25f;
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool logOnlyFirstProblem = true;
    [SerializeField] private float maxAllowedAbsPosition = 100000f;
    [SerializeField] private float maxAllowedAbsScale = 1000f;

    private float nextScanTime;
    private bool hasLoggedProblem;

    private void LateUpdate()
    {
        if (!scanEveryFrame)
            return;

        if (Time.unscaledTime < nextScanTime)
            return;

        nextScanTime = Time.unscaledTime + Mathf.Max(0.02f, scanIntervalSeconds);
        ScanNow();
    }

    [ContextMenu("Scan AABB Now")]
    public void ScanNow()
    {
        if (logOnlyFirstProblem && hasLoggedProblem)
            return;

        Renderer[] renderers = FindObjectsByType<Renderer>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            Transform t = r.transform;
            if (HasInvalidTransform(t, out string transformReason))
            {
                LogProblem($"Invalid transform on renderer '{r.name}': {transformReason}", r.gameObject);
                return;
            }

            Bounds b = r.bounds;
            if (HasInvalidBounds(b, out string boundsReason))
            {
                LogProblem($"Invalid bounds on renderer '{r.name}': {boundsReason}", r.gameObject);
                return;
            }
        }
    }

    private bool HasInvalidTransform(Transform t, out string reason)
    {
        reason = "";
        if (t == null)
        {
            reason = "transform is null";
            return true;
        }

        Vector3 p = t.position;
        Vector3 s = t.lossyScale;

        if (!IsFinite(p) || !IsFinite(s))
        {
            reason = $"non-finite values (pos={p}, scale={s})";
            return true;
        }

        if (Mathf.Abs(p.x) > maxAllowedAbsPosition || Mathf.Abs(p.y) > maxAllowedAbsPosition || Mathf.Abs(p.z) > maxAllowedAbsPosition)
        {
            reason = $"position too large (pos={p})";
            return true;
        }

        if (Mathf.Abs(s.x) > maxAllowedAbsScale || Mathf.Abs(s.y) > maxAllowedAbsScale || Mathf.Abs(s.z) > maxAllowedAbsScale)
        {
            reason = $"scale too large (scale={s})";
            return true;
        }

        return false;
    }

    private static bool HasInvalidBounds(Bounds b, out string reason)
    {
        reason = "";
        Vector3 c = b.center;
        Vector3 e = b.extents;

        if (!IsFinite(c) || !IsFinite(e))
        {
            reason = $"non-finite bounds (center={c}, extents={e})";
            return true;
        }

        if (e.x < 0f || e.y < 0f || e.z < 0f)
        {
            reason = $"negative extents (extents={e})";
            return true;
        }

        return false;
    }

    private static bool IsFinite(Vector3 v)
    {
        return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
    }

    private static bool IsFinite(float f)
    {
        return !float.IsNaN(f) && !float.IsInfinity(f);
    }

    private void LogProblem(string message, GameObject culprit)
    {
        hasLoggedProblem = true;
        Debug.LogError($"[EndMatchAabbDiagnostics] {message}", culprit);
    }
}
