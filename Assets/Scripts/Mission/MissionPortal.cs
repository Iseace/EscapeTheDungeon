using UnityEngine;

public class MissionPortal : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private bool enableFallbackOverlapCheck = true;
    [SerializeField] private float fallbackCheckInterval = 0.2f;
    [SerializeField] private Collider portalTriggerOverride;

    private Collider portalTrigger;
    private float nextFallbackCheckTime;

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
        if (MissionObjectiveManager.Instance != null && !MissionObjectiveManager.Instance.IsEscapeWindowOpen)
            return;

        PlayerSetup player = other.GetComponentInParent<PlayerSetup>();
        if (player == null) return;
        if (player.Object == null) return;
        if (player.HasEscaped) return;

        if (player.Object.HasStateAuthority)
        {
            player.HasEscaped = true;
            return;
        }

        if (player.Object.HasInputAuthority)
        {
            player.Rpc_RequestEscapePortal();
        }
    }

    private void CheckPlayersInsideBounds()
    {
        Bounds b = portalTrigger.bounds;
        Collider[] hits = Physics.OverlapBox(b.center, b.extents, portalTrigger.transform.rotation);
        if (hits == null || hits.Length == 0) return;

        for (int i = 0; i < hits.Length; i++)
        {
            TryProcessEscape(hits[i]);
        }
    }
}
