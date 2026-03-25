using UnityEngine;

public class EndMatchEscapeRunnerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float forwardSpeed = 4f;
    [SerializeField] private float lateralSpeed = 5f;
    [SerializeField] private bool allowInputOnX = true;
    [SerializeField] private bool allowInputOnZ = false;

    [Header("Bounds")]
    [SerializeField] private bool clampX = true;
    [SerializeField] private Vector2 xBounds = new Vector2(-6f, 6f);
    [SerializeField] private bool clampZ = false;
    [SerializeField] private Vector2 zBounds = new Vector2(0f, 220f);

    [Header("Input")]
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private string verticalAxis = "Vertical";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    public void Configure(float newForwardSpeed, float newLateralSpeed, Vector2 newXBounds, bool newAllowInputOnZ)
    {
        forwardSpeed = Mathf.Max(0f, newForwardSpeed);
        lateralSpeed = Mathf.Max(0f, newLateralSpeed);
        xBounds = newXBounds;
        allowInputOnZ = newAllowInputOnZ;
        clampX = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        float inputX = allowInputOnX ? Input.GetAxisRaw(horizontalAxis) : 0f;
        float inputZ = allowInputOnZ ? Input.GetAxisRaw(verticalAxis) : 0f;

        Vector3 movement = new Vector3(inputX * lateralSpeed, 0f, forwardSpeed + (inputZ * lateralSpeed));
        transform.position += movement * dt;

        Vector3 pos = transform.position;

        if (clampX)
            pos.x = Mathf.Clamp(pos.x, Mathf.Min(xBounds.x, xBounds.y), Mathf.Max(xBounds.x, xBounds.y));

        if (clampZ)
            pos.z = Mathf.Clamp(pos.z, Mathf.Min(zBounds.x, zBounds.y), Mathf.Max(zBounds.x, zBounds.y));

        transform.position = pos;

        if (debugLogs && (Mathf.Abs(inputX) > 0.01f || Mathf.Abs(inputZ) > 0.01f))
            Debug.Log($"[EndMatchEscapeRunnerController] Input x={inputX:0.00}, z={inputZ:0.00}, pos={transform.position}");
    }
}
