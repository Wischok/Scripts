using UnityEngine;

[System.Serializable]
public class Point
{
    ///
    /// --------- Private Variables ------------------------------------
    /// 
    
    /// Summary:
    ///     The position of the floor plan point. X and Y are the 2D points and Z is the height.
    [SerializeField] private Vector3 _position;

    ///
    /// ---------- Public Variables -------------------------------------
    /// 
    
    public Vector3 Position3D//3D position
    {
        get => _position;
        set => _position = value;
    }
    public Vector2 WorldViewPosition2D//2D position in world view, used for various calculations
    {
        get => new(_position.x, _position.y + _position.z * 0.5f);
    }
    public float X { get => _position.x; set => _position.x = value; }
    public float Y { get => _position.y; set => _position.y = value; }
    public float Z { get => _position.z; set => _position.z = value; }

    ///
    /// ---------- Constructors --------------------------------------
    /// 
    
    public Point(Vector3 position)
    {
        _position = position;
    }

    public Point(float x, float y, float height)
    {
        _position = new (x, y, height);
    }

    public Point(Vector2 position, float height)
    {
        _position = new (position.x, position.y, height);
    }
    
    public Point(float x, float y)
    {
        _position = new (x, y, 0f);
    }

    public Point(Vector2 position)
    {
        _position = new (position.x, position.y, 0f);
    }

    public Point() : this(Vector3.zero) { }
}
