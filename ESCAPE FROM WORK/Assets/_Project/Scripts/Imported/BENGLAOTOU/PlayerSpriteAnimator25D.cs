using UnityEngine;

[DisallowMultipleComponent]
public class PlayerSpriteAnimator25D : MonoBehaviour
{
    private enum Facing { Down, Up, Left, Right }

    [Header("Sources")]
    public PlayerMovement25D movementSource;
    public SpriteRenderer spriteRenderer;

    [Header("Idle Sprites")]
    public Sprite idleDown;
    public Sprite idleUp;
    public Sprite idleLeft;
    public Sprite idleRight;

    [Header("Walk Frames (left-to-right)")]
    public Sprite[] walkDownFrames;
    public Sprite[] walkUpFrames;
    public Sprite[] walkLeftFrames;
    public Sprite[] walkRightFrames;

    [Header("Playback")]
    public float walkFps = 8f;
    public bool animateOnlyWhenMoving = true;

    [Header("Mirroring")]
    [Tooltip("When true, Right facing uses idleLeft / walkLeftFrames with SpriteRenderer.flipX = true. When false, Right uses idleRight / walkRightFrames.")]
    public bool mirrorLeftForRight = true;

    private Facing _lastFacing = Facing.Down;
    private bool _wasMoving;
    private float _animTimer;

    private void Reset()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (movementSource == null) movementSource = GetComponentInParent<PlayerMovement25D>();
    }

    private void Update()
    {
        if (spriteRenderer == null || movementSource == null) return;

        Vector2 current = movementSource.CurrentMoveInput;
        Vector2 last    = movementSource.LastMoveInput;

        bool moving   = current.sqrMagnitude > 0.01f;
        Facing facing = ResolveFacing(last);

        // Reset frame timing when facing changes or when transitioning idle -> moving.
        if (facing != _lastFacing || (moving && !_wasMoving))
            _animTimer = 0f;

        // Drive horizontal flip based on facing + mirror mode.
        bool flip = mirrorLeftForRight && facing == Facing.Right;
        if (spriteRenderer.flipX != flip)
            spriteRenderer.flipX = flip;

        Sprite next = null;

        if (moving || (!animateOnlyWhenMoving && GetWalkFrames(facing) != null))
        {
            Sprite[] frames = GetWalkFrames(facing);
            if (frames != null && frames.Length > 0)
            {
                _animTimer += Time.deltaTime;
                float period = 1f / Mathf.Max(0.0001f, walkFps);
                int frame = Mathf.FloorToInt(_animTimer / period) % frames.Length;
                next = frames[frame];
            }
            else
            {
                next = GetIdleFor(facing);
            }
        }
        else
        {
            _animTimer = 0f;
            next = GetIdleFor(facing);
        }

        if (next != null && spriteRenderer.sprite != next)
            spriteRenderer.sprite = next;

        _lastFacing = facing;
        _wasMoving  = moving;
    }

    private Sprite[] GetWalkFrames(Facing f)
    {
        switch (f)
        {
            case Facing.Up:    return walkUpFrames;
            case Facing.Left:  return walkLeftFrames;
            case Facing.Right:
                if (mirrorLeftForRight) return walkLeftFrames;
                // fallback: use walkRightFrames if provided, otherwise mirror left anyway
                return (walkRightFrames != null && walkRightFrames.Length > 0) ? walkRightFrames : walkLeftFrames;
            default:           return walkDownFrames;
        }
    }

    private static Facing ResolveFacing(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return Facing.Down;
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return dir.x < 0f ? Facing.Left : Facing.Right;
        return dir.y > 0f ? Facing.Up : Facing.Down;
    }

    private Sprite GetIdleFor(Facing f)
    {
        switch (f)
        {
            case Facing.Up:    return idleUp   != null ? idleUp   : idleDown;
            case Facing.Left:  return idleLeft != null ? idleLeft : idleDown;
            case Facing.Right:
                if (mirrorLeftForRight)
                    return idleLeft != null ? idleLeft : idleDown;
                return idleRight != null ? idleRight : (idleLeft != null ? idleLeft : idleDown);
            default:           return idleDown;
        }
    }
}
