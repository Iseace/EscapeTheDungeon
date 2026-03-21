using UnityEngine;

public class NameCamera : MonoBehaviour
{
    private Camera _cam;

    private void Start()
    {
        _cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (_cam == null)
            _cam = Camera.main;

        if (_cam != null)
            transform.rotation = _cam.transform.rotation;
    }
}