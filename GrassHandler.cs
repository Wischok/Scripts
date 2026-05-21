using UnityEngine;
using UnityEngine.Tilemaps;

public class GrassHandler : MonoBehaviour
{
    public Tilemap grassTilemap; // Reference to the Tilemap component
    public Tile grassTile;

    [SerializeField, Range(1, 100)] private int mapWidth;
    [SerializeField, Range(1, 100)] private int mapHeight;

    //a function that runs in the inspector
    [ContextMenu("Spawn Grass")]
    private void SpawnGrass()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                grassTilemap.SetTile(new Vector3Int(x, y, 0), grassTile); // Create a new tile
            }
        }
    }
}
