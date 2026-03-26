using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LandmineBehaviour : NetworkBehaviour
{
    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 12f;
    [SerializeField] private float knockbackUpwardBoost = 2f;

    [Header("Lifetime")]
    [SerializeField] private bool despawnAfterTrigger = true;
    [SerializeField] private float despawnDelaySeconds = 0.1f;

    [Header("Visuals")]
    [SerializeField] private GameObject explosionVfxPrefab;

    [Networked, OnChangedRender(nameof(OnTriggeredChanged))]
    private NetworkBool IsTriggered { get; set; }

    [Networked]
    private TickTimer DespawnTimer { get; set; }

    private Collider mineCollider;
    private Renderer[] mineRenderers;

    public override void Spawned()
    {
        mineCollider = GetComponent<Collider>();
        mineRenderers = GetComponentsInChildren<Renderer>(true);
        ApplyTriggeredState((bool)IsTriggered);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority || IsTriggered)
        {
            return;
        }

        BroomMove broomMove = other.GetComponentInParent<BroomMove>();
        if (broomMove == null)
        {
            return;
        }

        ApplyKnockbackToRacer(broomMove.transform);
        RPC_PlayExplosion(transform.position);

        if (despawnAfterTrigger)
        {
            IsTriggered = true;
            DespawnTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0f, despawnDelaySeconds));
        }
    }

    private void ApplyKnockbackToRacer(Transform racerTransform)
    {
        if (racerTransform == null)
        {
            return;
        }

        Rigidbody racerRb = racerTransform.GetComponent<Rigidbody>();
        if (racerRb == null)
        {
            return;
        }

        Vector3 away = racerTransform.position - transform.position;
        away.y = 0f;

        if (away.sqrMagnitude < 0.0001f)
        {
            away = racerTransform.forward;
        }

        Vector3 impulse = away.normalized * knockbackForce;
        impulse.y = knockbackUpwardBoost;
        racerRb.AddForce(impulse, ForceMode.VelocityChange);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !despawnAfterTrigger || !IsTriggered)
        {
            return;
        }

        if (DespawnTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }

    private void OnTriggeredChanged()
    {
        ApplyTriggeredState((bool)IsTriggered);
    }

    private void ApplyTriggeredState(bool triggered)
    {
        bool shouldDisableMine = triggered && despawnAfterTrigger;

        if (mineCollider != null)
        {
            mineCollider.enabled = !shouldDisableMine;
        }

        if (mineRenderers == null)
        {
            return;
        }

        for (int i = 0; i < mineRenderers.Length; i++)
        {
            mineRenderers[i].enabled = !shouldDisableMine;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayExplosion(Vector3 worldPosition)
    {
        if (explosionVfxPrefab == null)
        {
            return;
        }

        Instantiate(explosionVfxPrefab, worldPosition, Quaternion.identity);
    }
}
