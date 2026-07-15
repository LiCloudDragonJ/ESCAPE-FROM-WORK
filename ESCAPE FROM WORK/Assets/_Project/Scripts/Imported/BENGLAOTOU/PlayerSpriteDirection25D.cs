using UnityEngine;

[DisallowMultipleComponent]
public class PlayerSpriteDirection25D : MonoBehaviour
{
    [Header("Sources")]
    public PlayerMovement25D movementSource;
    public SpriteRenderer spriteRenderer;

    [Header("Direction Sprites")]
    public Sprite downSprite;
    public Sprite upSprite;
    public Sprite leftSprite;
    public Sprite rightSprite;

    private void Reset()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (movementSource == null) movementSource = GetComponentInParent<PlayerMovement25D>();
    }

    private void Start()
    {
        ApplyDirection(movementSource != null ? movementSource.LastMoveInput : Vector2.down);
    }

    private void Update()
    {
        if (spriteRenderer == null) return;

        Vector2 dir = movementSource != null ? movementSource.LastMoveInput : Vector2.zero;
        ApplyDirection(dir);
    }

    private void ApplyDirection(Vector2 dir)
    {
        Sprite target = downSprite;

        if (dir.sqrMagnitude > 0.0001f)
        {
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
                target = dir.x < 0f ? leftSprite : rightSprite;
            else
                target = dir.y > 0f ? upSprite : downSprite;
        }

        if (target != null && spriteRenderer.sprite != target)
            spriteRenderer.sprite = target;
    }
}
