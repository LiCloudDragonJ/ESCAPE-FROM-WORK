using UnityEngine;

[DisallowMultipleComponent]
public class PlayerVisualMotion25D : MonoBehaviour
{
    [Header("Sources")]
    public PlayerMovement25D movementSource;
    public Transform visualRoot;

    [Header("Bob")]
    public float bobAmplitude = 0.035f;
    public float bobSpeed = 10f;

    [Header("Return")]
    public float returnSpeed = 12f;

    private Vector3 _initialLocalPos;
    private bool _initialised;

    private void Reset()
    {
        if (movementSource == null) movementSource = GetComponentInParent<PlayerMovement25D>();
        if (visualRoot == null) visualRoot = transform;
    }

    private void Awake()
    {
        if (visualRoot == null) visualRoot = transform;
        _initialLocalPos = visualRoot.localPosition;
        _initialised = true;
    }

    private void Update()
    {
        if (!_initialised || visualRoot == null) return;

        bool moving = movementSource != null &&
                      movementSource.CurrentMoveInput.sqrMagnitude > 0.01f;

        if (moving)
        {
            float y = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            visualRoot.localPosition = _initialLocalPos + new Vector3(0f, y, 0f);
        }
        else
        {
            visualRoot.localPosition = Vector3.Lerp(
                visualRoot.localPosition,
                _initialLocalPos,
                returnSpeed * Time.deltaTime);
        }
    }
}
