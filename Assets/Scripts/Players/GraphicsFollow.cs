using UnityEngine;
using Fusion;

public class GraphicsFollow : SimulationBehaviour
{
    public Transform target;

    void LateUpdate()
    {
        if (!target) return;

        transform.position = target.position;
        transform.rotation = Quaternion.Euler(
            0f,
            target.eulerAngles.y,
            0f
        );
    }
}