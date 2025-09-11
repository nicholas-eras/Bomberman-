using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BombController : MonoBehaviour
{
    [Header("Bomb")]
    public KeyCode inputKey = KeyCode.LeftShift;
    public GameObject bombPrefab;
    public float bombFuseTime = 2f;
    public int bombAmount = 1;
    private int bombsRemaining;

    [Header("Explosion")]
    public Explosion explosionPrefab;
    public LayerMask explosionLayerMask;
    public float explosionDuration = 1f;
    public int explosionRadius = 2;

    [Header("Destructible")]
    public Tilemap destructibleTiles;
    public Tilemap undestructibleTiles;
    public Destructible destructiblePrefab;
    public Destructible itemDestructiblePrefab;

    [Header("Scenary")]
    public Tilemap scenary;

    private void OnEnable()
    {
        bombsRemaining = bombAmount;
    }

    private void Update()
    {
        if (bombsRemaining > 0 && Input.GetKeyDown(inputKey))
        {
            StartCoroutine(PlaceBomb());
        }
    }
    
    // private void DebugAnchors(GameObject obj, string name)
    // {
    //     SpriteRenderer sr = obj.GetComponentInChildren<SpriteRenderer>(); // pega inclusive filhos
    //     if (sr != null)
    //     {
    //         Vector3 center = sr.bounds.center;
    //         Vector3 bottomCenter = new Vector3(center.x, sr.bounds.min.y, center.z);

    //         Debug.Log($"{name} -> Pivot (Transform.position): {obj.transform.position}");
    //         Debug.Log($"{name} -> Bounds Center: {center}");
    //         Debug.Log($"{name} -> Bottom Center: {bottomCenter}");

    //         Debug.DrawRay(obj.transform.position, Vector3.up * 0.5f, Color.green, 2f); // pivot
    //         Debug.DrawRay(center, Vector3.up * 0.5f, Color.blue, 2f); // center
    //         Debug.DrawRay(bottomCenter, Vector3.up * 0.5f, Color.red, 2f); // bottom center
    //     }
    //     else
    //     {
    //         Debug.LogWarning($"{name} não tem SpriteRenderer nem nos filhos!");
    //     }
    // }
    
    private IEnumerator PlaceBomb()
    {
        // pega a posição do player
        Vector3 playerPosition = transform.position;

        // converte para a célula do tilemap scenary
        Vector3Int cell = scenary.WorldToCell(playerPosition);

        // pega o centro dessa célula no mundo
        Vector3 cellCenter = scenary.GetCellCenterWorld(cell);

        // instancia a bomba no centro da célula
        GameObject bombObj = Instantiate(bombPrefab, cellCenter, Quaternion.identity);

        bombsRemaining--;

        // inicializa a bomba
        Bomb bomb = bombObj.GetComponent<Bomb>();
        bomb.Init(bombFuseTime, explosionRadius, explosionDuration, explosionPrefab,
                explosionLayerMask, destructibleTiles, undestructibleTiles,
                destructiblePrefab, itemDestructiblePrefab);

        // espera até a bomba ser destruída
        while (bombObj != null)
            yield return null;

        bombsRemaining++;
    }

    private void ClearDestructible(Vector2 position)
    {
        Vector3Int cell = destructibleTiles.WorldToCell(position);
        TileBase tile = destructibleTiles.GetTile(cell);

        if (tile != null)
        {
            Instantiate(destructiblePrefab, position, Quaternion.identity);
            destructibleTiles.SetTile(cell, null);
        }
    }

    public void AddBomb()
    {
        bombAmount++;
        bombsRemaining++;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Bomb"))
        {
            other.isTrigger = false;
        }
    }
}
