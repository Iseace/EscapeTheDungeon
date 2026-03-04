using UnityEngine;

/// <summary>
/// Controls the Main Menu camera.
/// Smoothly transitions from the initial "title" position down to section
/// positions defined by empty GameObjects, and slides left/right between them.
/// </summary>
public class MenuCamera : MonoBehaviour
{
    [Header("Sections (empty GameObjects in scene)")]
    [Tooltip("Ordered list of positions the camera can travel to. " +
             "Element 0 is where the camera goes after pressing Play.")]
    public Transform[] sectionPoints;

    [Header("Transition Settings")]
    [Tooltip("How fast the camera moves to the target position.")]
    public float moveSpeed = 3f;
    [Tooltip("How fast the camera rotates to match the target rotation.")]
    public float rotateSpeed = 3f;

    // ── state ──────────────────────────────────────────────
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private int _currentSectionIndex = 0;
    private bool _isAtMenu = true;   // still showing the title screen
    private bool _isMoving = false;

    // target the camera is lerping towards
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;

    // ── public read-only helpers for MenuController ────────
    public bool IsAtMenu => _isAtMenu;
    public bool IsMoving => _isMoving;
    public int CurrentSection => _currentSectionIndex;
    public int SectionCount => sectionPoints == null ? 0 : sectionPoints.Length;

    // ── events ─────────────────────────────────────────────
    /// <summary>Fired when the camera finishes arriving at a section.</summary>
    public event System.Action<int> OnSectionReached;

    // ────────────────────────────────────────────────────────
    void Start()
    {
        // remember the initial camera pose (the "title" view)
        _startPosition = transform.position;
        _startRotation = transform.rotation;

        _targetPosition = _startPosition;
        _targetRotation = _startRotation;
    }

    void Update()
    {
        if (!_isMoving) return;

        transform.position = Vector3.Lerp(transform.position, _targetPosition, moveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, rotateSpeed * Time.deltaTime);

        // snap when close enough
        if (Vector3.Distance(transform.position, _targetPosition) < 0.01f &&
            Quaternion.Angle(transform.rotation, _targetRotation) < 0.1f)
        {
            transform.position = _targetPosition;
            transform.rotation = _targetRotation;
            _isMoving = false;

            if (!_isAtMenu)
                OnSectionReached?.Invoke(_currentSectionIndex);
        }
    }

    // ── public API ─────────────────────────────────────────

    /// <summary>Move from the title view down to the first section (called by Play button).</summary>
    public void EnterSections()
    {
        if (sectionPoints == null || sectionPoints.Length == 0) return;

        _isAtMenu = false;
        _currentSectionIndex = 0;
        SetTarget(sectionPoints[0]);
    }

    /// <summary>Move one section to the right (+1), wrapping to the first section at the end.</summary>
    public void NextSection()
    {
        if (_isAtMenu || sectionPoints.Length == 0) return;

        _currentSectionIndex = (_currentSectionIndex + 1) % sectionPoints.Length;
        SetTarget(sectionPoints[_currentSectionIndex]);
    }

    /// <summary>Move one section to the left (−1), wrapping to the last section at the start.</summary>
    public void PreviousSection()
    {
        if (_isAtMenu || sectionPoints.Length == 0) return;

        _currentSectionIndex = (_currentSectionIndex - 1 + sectionPoints.Length) % sectionPoints.Length;
        SetTarget(sectionPoints[_currentSectionIndex]);
    }

    /// <summary>Return the camera to the original title-screen position.</summary>
    public void ReturnToMenu()
    {
        _isAtMenu = true;
        _targetPosition = _startPosition;
        _targetRotation = _startRotation;
        _isMoving = true;
    }

    // ── private helpers ────────────────────────────────────
    private void SetTarget(Transform point)
    {
        _targetPosition = point.position;
        _targetRotation = point.rotation;
        _isMoving = true;
    }
}
