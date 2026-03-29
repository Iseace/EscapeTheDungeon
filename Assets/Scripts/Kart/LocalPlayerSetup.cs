using UnityEngine;
using Fusion; // Make sure we are using the Fusion library

// We inherit from NetworkBehaviour so we can use Fusion's Spawned() method
public class LocalPlayerSetup : NetworkBehaviour
{
    [Header("Camera Setup")]
    [Tooltip("Drag the Camera Socket from inside THIS prefab here")]
    public Transform myCameraSocket;

    [Header("Character Skin Setup")]
    [Tooltip("Optional. If empty, script will auto-find child named 'SeatAnchor'.")]
    [SerializeField] private Transform seatAnchor;

    [Networked]
    public int SelectedCharacterIndex { get; set; }

    // This runs automatically when Fusion spawns this object over the network
    public override void Spawned()
    {
        ResolveSeatAnchor();

        // CRITICAL: We only want the camera to follow THIS player if this instance
        // belongs to the local player playing on this computer.
        if (HasInputAuthority)
        {
            int savedCharacter = PlayerPrefs.GetInt("SelectedCharacterID", 0);
            Rpc_RequestCharacterSelection(savedCharacter);

            // Find the Main Camera in the scene
            Camera mainCam = Camera.main;

            if (mainCam != null)
            {
                // Get our CameraFollow script from it
                CameraFollow camFollow = mainCam.GetComponent<CameraFollow>();

                if (camFollow != null)
                {
                    // Hand over the socket!
                    camFollow.SetTarget(myCameraSocket);
                }
            }
            else
            {
                Debug.LogError("No Main Camera found! Make sure your camera has the 'MainCamera' tag.");
            }
        }

        ApplySelectedSkin();
    }

    public override void Render()
    {
        ApplySelectedSkin();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_RequestCharacterSelection(int index)
    {
        SelectedCharacterIndex = index;
    }

    private void ResolveSeatAnchor()
    {
        if (seatAnchor != null) return;

        Transform autoFound = transform.Find("VisualRoot/SeatAnchor");
        if (autoFound == null)
            autoFound = transform.Find("SeatAnchor");

        seatAnchor = autoFound;
    }

    private void ApplySelectedSkin()
    {
        if (seatAnchor == null || seatAnchor.childCount == 0) return;

        int count = seatAnchor.childCount;
        int normalizedIndex = NormalizeIndex(SelectedCharacterIndex, count);

        for (int i = 0; i < count; i++)
        {
            Transform skin = seatAnchor.GetChild(i);
            if (skin == null) continue;

            bool shouldBeActive = (i == normalizedIndex);
            if (skin.gameObject.activeSelf != shouldBeActive)
            {
                skin.gameObject.SetActive(shouldBeActive);
            }
        }
    }

    private static int NormalizeIndex(int index, int total)
    {
        if (total <= 0) return 0;

        int mod = index % total;
        return mod < 0 ? mod + total : mod;
    }
}