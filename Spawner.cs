/// Description: Spawner Class: Spawns the given entity prefab at the given
/// spawn point. Capable at spawning entities at random intervals or on demand.
/// 
/// Author: Aaron Evans
/// Created: 3/18/2026
using UnityEngine;

public class Spawner : MonoBehaviour
{
    /// Summary: 
    ///     The prefab for the entity / object to be spawned
    [SerializeField] private GameObject entityPrefab;

    /// Summary:
    ///     The point at which the entity will be spawned. This should be a
    ///     child of the spawner object.
    [SerializeField] private Transform spawnPoint;

    /// Summary:
    ///     Whether the spawner should spawn entities at random intervals or on demand.
    [SerializeField] private bool spawnAtRandomIntervals = false;

    /// Summary:
    ///    The minimum and maximum time between spawns when spawning at random intervals.
    [SerializeField] private float minSpawnTime = 1f;
    [SerializeField] private float maxSpawnTime = 5f;

    //singleton
    private static Spawner _instance;
    public static Spawner Instance => _instance;

    private void Awake()
    {
        //singleton logic
        if (_instance != null && _instance != this)
        {
            Destroy(this);
        }
        else
        {
            _instance = this;
        }
    }

    /// Summary:
    ///     Spawn the entity at the spawn point.
    [ContextMenu("Spawn Entity")]
    public void SpawnEntity() => Instantiate(entityPrefab, spawnPoint.position, Quaternion.identity);
    public void SpawnEntity(Vector3 position) => Instantiate(entityPrefab, position, Quaternion.identity);
}
