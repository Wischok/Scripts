using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
public class HeightMap : MonoBehaviour
{
    //singleton
    private static HeightMap m_instance;
    public static HeightMap Instance => m_instance;

    //tilemap
    private Tilemap m_map;
    public Tilemap Map => m_map;

    //sprite legend
    [Header("--- HeightMap Sprite Legend ---")]
    [SerializeField] private List<Sprite> spriteList;

    //organize sprites into dictionary for quick retrieval
    private Dictionary<string, Sprite> spriteKeyList;

    private void Awake()
    {
        //initialize singleton
        m_instance = this;

        //grab components
        m_map = GetComponent<Tilemap>();
    }

    private void Start()
    {
        spriteList.Reverse();
    }

    public int GetHeight(Vector3 position)
    {
        // Convert the world position to cell coordinates
        Vector3Int cellPosition = m_map.WorldToCell(position);
        Sprite sprite = m_map.GetSprite(cellPosition);

        //height int
        int height = 0;

        if(sprite == null)
        {
            return height;
        }

        for(int i = 0; i < spriteList.Count; i++)
        {
            if(sprite == spriteList[i])
            {
                height = ++i;
                break;
            }
        }

        return height; 
    }
}
