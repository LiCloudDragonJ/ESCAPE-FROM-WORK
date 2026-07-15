using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement25D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;

    [Header("Gravity")]
    public float gravity = -20f;
    public float groundedStickVelocity = -2f;

    public Vector2 CurrentMoveInput { get; private set; }
    public Vector2 LastMoveInput { get; private set; } = Vector2.down;

    private CharacterController _controller;
    private HD2DVisualClearanceProbe _visualClearanceProbe;
    private float _verticalVelocity;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _visualClearanceProbe = GetComponent<HD2DVisualClearanceProbe>();
    }

    private void Update()
    {
        Vector2 input = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed) input.x -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.wKey.isPressed) input.y += 1f;
        }

        if (input.sqrMagnitude > 1f)
            input.Normalize();

        CurrentMoveInput = input;
        if (input.sqrMagnitude > 0.01f)
            LastMoveInput = input;

        // World-space movement (fixed camera): W = +Z forward, S = -Z, A = -X, D = +X
        Vector3 moveDir = new Vector3(input.x, 0f, input.y);

        Vector3 horizontalDelta = moveDir * moveSpeed * Time.deltaTime;

        if (_visualClearanceProbe != null)
            horizontalDelta = _visualClearanceProbe.FilterHorizontalDelta(transform.position, horizontalDelta);

        if (_controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = groundedStickVelocity;

        _verticalVelocity += gravity * Time.deltaTime;

        Vector3 verticalDelta = new Vector3(0f, _verticalVelocity * Time.deltaTime, 0f);
        _controller.Move(horizontalDelta + verticalDelta);
    }
}
