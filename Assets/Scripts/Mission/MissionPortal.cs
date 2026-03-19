using UnityEngine;

public class MissionPortal : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private bool enableFallbackOverlapCheck = true;
    [SerializeField] private float fallbackCheckInterval = 0.2f;
    [SerializeField] private LayerMask fallbackDetectionMask = ~0;
    [SerializeField] private QueryTriggerInteraction fallbackQueryTriggerInteraction = QueryTriggerInteraction.Collide;
    [SerializeField] private Collider portalTriggerOverride;
    [SerializeField] private bool debugLogs;

    private Collider portalTrigger;
    private float nextFallbackCheckTime;
    private readonly Collider[] fallbackHitsBuffer = new Collider[64];

    private void Awake()
    {
        portalTrigger = ResolvePortalTrigger();
        if (portalTrigger == null)
        {
            Debug.LogError("[MissionPortal] Missing Collider component (self or children).");
            enabled = false;
            return;
        }

        if (!portalTrigger.isTrigger)
        {
            portalTrigger.isTrigger = true;
        }

        // Trigger events are more reliable when one of the colliders has a Rigidbody.
        var rb = portalTrigger.attachedRigidbody;
        if (rb == null)
        {
            rb = portalTrigger.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private Collider ResolvePortalTrigger()
    {
        if (portalTriggerOverride != null) return portalTriggerOverride;

        Collider local = GetComponent<Collider>();
        if (local != null) return local;

        return GetComponentInChildren<Collider>(true);
    }

    private void Update()
    {
        if (!enableFallbackOverlapCheck || portalTrigger == null) return;
        if (Time.time < nextFallbackCheckTime) return;

        nextFallbackCheckTime = Time.time + Mathf.Max(0.05f, fallbackCheckInterval);
        CheckPlayersInsideBounds();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryProcessEscape(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryProcessEscape(other);
    }

    private void TryProcessEscape(Collider other)
    {
        MissionObjectiveManager missionManager = MissionObjectiveManager.Instance;
        if (missionManager != null && !missionManager.IsEscapeWindowOpen)
            return;

        if (other == null) return;

        PlayerSetup player = other.GetComponentInParent<PlayerSetup>();
        if (player == null) return;
        if (player.Object == null) return;
        if (player.IsBossPlayer()) return;
        if (player.HasEscaped) return;

        if (debugLogs)
        {
            Debug.Log($"[MissionPortal] Escape requested for {player.name} by collider {other.name}.");
        }

        // Always route through RPC so StateAuthority is the single writer of HasEscaped.
        player.Rpc_RequestEscapePortal();
    }

    private void CheckPlayersInsideBounds()
    {
        int hitCount = 0;

        if (portalTrigger is BoxCollider box)
        {
            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, Abs(box.transform.lossyScale));
            hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, fallbackHitsBuffer, box.transform.rotation, fallbackDetectionMask, fallbackQueryTriggerInteraction);
        }
        else if (portalTrigger is SphereCollider sphere)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center);
            float maxScale = Mathf.Max(Mathf.Abs(sphere.transform.lossyScale.x), Mathf.Abs(sphere.transform.lossyScale.y), Mathf.Abs(sphere.transform.lossyScale.z));
            float radius = sphere.radius * maxScale;
            hitCount = Physics.OverlapSphereNonAlloc(center, radius, fallbackHitsBuffer, fallbackDetectionMask, fallbackQueryTriggerInteraction);
        }
        else if (portalTrigger is CapsuleCollider capsule)
        {
            GetWorldCapsule(capsule, out Vector3 p0, out Vector3 p1, out float radius);
            hitCount = Physics.OverlapCapsuleNonAlloc(p0, p1, radius, fallbackHitsBuffer, fallbackDetectionMask, fallbackQueryTriggerInteraction);
        }
        else
        {
            Bounds b = portalTrigger.bounds;
            hitCount = Physics.OverlapBoxNonAlloc(b.center, b.extents, fallbackHitsBuffer, portalTrigger.transform.rotation, fallbackDetectionMask, fallbackQueryTriggerInteraction);
        }

        if (hitCount <= 0) return;

        for (int i = 0; i < hitCount; i++)
        {
            TryProcessEscape(fallbackHitsBuffer[i]);
        }
    }

    private static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private static void GetWorldCapsule(CapsuleCollider capsule, out Vector3 p0, out Vector3 p1, out float radius)
    {
        Transform t = capsule.transform;
        Vector3 center = t.TransformPoint(capsule.center);
        Vector3 lossy = Abs(t.lossyScale);

        int dir = capsule.direction;
        float axisScale = dir == 0 ? lossy.x : (dir == 1 ? lossy.y : lossy.z);
        float rScaleA = dir == 0 ? lossy.y : lossy.x;
        float rScaleB = dir == 2 ? lossy.y : lossy.z;

        radius = capsule.radius * Mathf.Max(rScaleA, rScaleB);
        float height = Mathf.Max(capsule.height * axisScale, radius * 2f);
        float halfLine = Mathf.Max(0f, (height * 0.5f) - radius);

        Vector3 axis = dir == 0 ? t.right : (dir == 1 ? t.up : t.forward);
        p0 = center + axis * halfLine;
        p1 = center - axis * halfLine;
    }
}
