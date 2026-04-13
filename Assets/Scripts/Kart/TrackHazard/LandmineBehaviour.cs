using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LandmineBehaviour : NetworkBehaviour
{
    [Header("Classification")]
    [SerializeField] private bool isLandmine = false;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 12f;
    [SerializeField] private float knockbackUpwardBoost = 2f;

    [Header("Lifetime")]
    [SerializeField] private bool despawnAfterTrigger = true;
    [SerializeField] private float despawnDelaySeconds = 0.1f;
    [SerializeField] private bool respawnAfterTrigger = false;
    [SerializeField] private float respawnDelaySeconds = 10f;

    private const float LandmineRespawnSeconds = 10f;

    [Header("Visuals")]
    [SerializeField] private GameObject explosionVfxPrefab;

    [Networked, OnChangedRender(nameof(OnTriggeredChanged))]
    private NetworkBool IsTriggered { get; set; }

    [Networked]
    private TickTimer DespawnTimer { get; set; }

    [Networked]
    private TickTimer RespawnTimer { get; set; }

    [Networked]
    private NetworkBool HasSpawnPose { get; set; }

    [Networked]
    private Vector3 SpawnPosition { get; set; }

    [Networked]
    private Vector3 SpawnEulerAngles { get; set; }

    [Networked]
    private Vector3 SpawnLocalScale { get; set; }

    private Collider mineCollider;
    private Renderer[] mineRenderers;

    public override void Spawned()
    {
        mineCollider = GetComponent<Collider>();
        mineRenderers = GetComponentsInChildren<Renderer>(true);

        if (HasStateAuthority && !HasSpawnPose)
        {
            HasSpawnPose = true;
            SpawnPosition = transform.position;
            SpawnEulerAngles = transform.eulerAngles;
            SpawnLocalScale = transform.localScale;
        }

        ApplyNetworkedSpawnPose();
        ApplyTriggeredState((bool)IsTriggered);
    }

    public override void Render()
    {
        ApplyNetworkedSpawnPose();
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

        bool shouldDespawnAfterTrigger = despawnAfterTrigger || isLandmine;
        bool shouldRespawnAfterTrigger = isLandmine || respawnAfterTrigger;
        float currentRespawnDelaySeconds = isLandmine ? LandmineRespawnSeconds : respawnDelaySeconds;

        if (shouldDespawnAfterTrigger)
        {
            IsTriggered = true;

            if (shouldRespawnAfterTrigger)
            {
                RespawnTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0f, currentRespawnDelaySeconds));
                DespawnTimer = TickTimer.None;
            }
            else
            {
                DespawnTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(0f, despawnDelaySeconds));
                RespawnTimer = TickTimer.None;
            }
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
        bool shouldDespawnAfterTrigger = despawnAfterTrigger || isLandmine;
        bool shouldRespawnAfterTrigger = isLandmine || respawnAfterTrigger;

        if (!HasStateAuthority || !shouldDespawnAfterTrigger || !IsTriggered)
        {
            return;
        }

        if (shouldRespawnAfterTrigger)
        {
            if (RespawnTimer.Expired(Runner))
            {
                IsTriggered = false;
                RespawnTimer = TickTimer.None;
            }

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

    private void ApplyNetworkedSpawnPose()
    {
        if (!HasSpawnPose)
        {
            return;
        }

        Quaternion spawnRotation = Quaternion.Euler(SpawnEulerAngles);

        if ((transform.localScale - SpawnLocalScale).sqrMagnitude > 0.0001f)
        {
            transform.localScale = SpawnLocalScale;
        }

        if ((transform.position - SpawnPosition).sqrMagnitude > 0.0001f || Quaternion.Angle(transform.rotation, spawnRotation) > 0.1f)
        {
            transform.SetPositionAndRotation(SpawnPosition, spawnRotation);
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
