using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Shadow : MonoBehaviour
{
    
    ///
    /// --------- Private Variables -------------------------------------
    ///

    /// Summary:
    ///     parent Position
    private Position _pPos;

    /// Summary:
    ///     sprite renderer component
    private SpriteRenderer _spriteRenderer;

    ///
    /// --------- Unity Methods -----------------------------------------
    /// 

    private void Awake() => _spriteRenderer = GetComponent<SpriteRenderer>();

    private void Update()
    {
        if (_pPos == null) return;

        Vector4 pos = _pPos.Pos_4D;

        // Shadow sits at the foot position on the elevated surface:
        // view Y = world Y + 0.5 * elevation (w), matching Pos_2D without jump height (z).
        transform.position = new Vector3(pos.x, pos.y + pos.w * 0.5f, 0f);
    }

    public void SetPosition(Position parentPosition) => _pPos = parentPosition;
}
