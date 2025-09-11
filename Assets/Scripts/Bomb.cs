using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Bomb : MonoBehaviour
{
    public float fuseTime;
    public int explosionRadius;
    private float explosionDuration;
    private Explosion explosionPrefab;
    private Explosion itemExplosionPrefab;
    private LayerMask explosionLayerMask;
    private Tilemap destructibleTiles;
    private Tilemap undestructibleTiles;
    private Destructible destructiblePrefab;
    private Destructible itemDestructiblePrefab;

    private Rigidbody2D rb;
    private bool isMoving;
    private Vector2 moveDirection;
    public float moveSpeed = 20f;
    private Vector2 playerPosition;
    private IEnumerator fuseCoroutine;

    public void Init(
        float fuseTime,
        int explosionRadius,
        float explosionDuration,
        Explosion explosionPrefab,
        LayerMask explosionLayerMask,
        Tilemap destructibleTiles,
        Tilemap undestructibleTiles,
        Destructible destructiblePrefab,
        Destructible itemDestructiblePrefab
    )
    {
        this.fuseTime = fuseTime;
        this.explosionRadius = explosionRadius;
        this.explosionDuration = explosionDuration;
        this.explosionPrefab = explosionPrefab;
        this.explosionLayerMask = explosionLayerMask;
        this.destructibleTiles = destructibleTiles;
        this.undestructibleTiles = undestructibleTiles;
        this.destructiblePrefab = destructiblePrefab;
        this.itemDestructiblePrefab = itemDestructiblePrefab;

        rb = GetComponent<Rigidbody2D>();

        fuseCoroutine = ExplodeAfterDelay();
        StartCoroutine(fuseCoroutine);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Se uma explosão encostar na bomba, ela detona antes do tempo
        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            StopCoroutine(fuseCoroutine);
            Explode();
        }
    }

    private void Update()
    {
        if (isMoving)
        {
            Vector2 nextPos = rb.position + moveDirection * moveSpeed * Time.deltaTime;

            Collider2D hit = Physics2D.OverlapBox(nextPos, Vector2.one * 0.8f, 0f, explosionLayerMask);
            if (hit != null)
            {
                isMoving = false;
                rb.velocity = Vector2.zero;
                return;
            }

            rb.MovePosition(nextPos);
        }
    }

    private IEnumerator ExplodeAfterDelay()
    {
        yield return new WaitForSeconds(fuseTime);
        Explode();
    }
    
    private void Explode()
    {
        // Cria explosão central - esta deve usar o sprite "start" apenas
        Vector2 position = (Vector2)transform.position;
        Explosion centerExplosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        centerExplosion.SetActiveRenderer(centerExplosion.start);
        centerExplosion.DestroyAfter(explosionDuration);

        // Cria explosões nas 4 direções, começando da próxima célula
        DoExplode(position + Vector2.up, Vector2.up, explosionRadius);
        DoExplode(position + Vector2.down, Vector2.down, explosionRadius);
        DoExplode(position + Vector2.left, Vector2.left, explosionRadius);
        DoExplode(position + Vector2.right, Vector2.right, explosionRadius);

        Destroy(gameObject);
    }

    private void DoExplode(Vector2 position, Vector2 direction, int length)
    {
        if (length <= 0) return;

        Collider2D hit = Physics2D.OverlapBox(position, Vector2.one / 2f, 0f, explosionLayerMask);

        if (hit != null)
        {
            // Se for indestrutível → para sem explosão
            if (hit.gameObject.layer == LayerMask.NameToLayer("Indestructible"))
            {
                return;
            }

            // Se for destrutível → cria explosão, destrói e para
            if (hit.gameObject.layer == LayerMask.NameToLayer("Destructible"))
            {
                Explosion explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
                explosion.SetActiveRenderer(length == 1 ? explosion.end : explosion.middle);
                explosion.SetDirection(direction);
                explosion.DestroyAfter(explosionDuration);

                ClearDestructible(position, "brick"); // remove o bloco
                return;
            }

            // Se for ditem → cria explosão, destrói e para
            if (hit.gameObject.layer == LayerMask.NameToLayer("Item"))
            {
                Explosion explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
                explosion.SetActiveRenderer(length == 1 ? explosion.end : explosion.middle);
                explosion.SetDirection(direction);
                explosion.DestroyAfter(explosionDuration);
                Destroy(hit.gameObject);

                ClearDestructible(position, "item"); // remove o bloco
                return;
            }

            // Qualquer outra coisa → para sem continuar
            return;
        }
        else
        {
            Explosion normalExplosion = Instantiate(explosionPrefab, position, Quaternion.identity);
            normalExplosion.SetActiveRenderer(length == 1 ? normalExplosion.end : normalExplosion.middle);
            normalExplosion.SetDirection(direction);
            normalExplosion.DestroyAfter(explosionDuration);

            DoExplode(position + direction, direction, length - 1);
        }       
    }

    private void ClearDestructible(Vector2 position, string cellType)
    {
        Vector3Int cell = destructibleTiles.WorldToCell(position);
        TileBase destructibleTile = destructibleTiles.GetTile(cell);

        if (destructibleTile != null)
        {
            Vector3 cellCenter = destructibleTiles.GetCellCenterWorld(cell);
            if (cellType == "brick")
            {
                Instantiate(destructiblePrefab, cellCenter, Quaternion.identity);
            }
            destructibleTiles.SetTile(cell, null);
        }
        if (cellType == "item")
        {
            Vector3 cellCenter = destructibleTiles.GetCellCenterWorld(cell);
            Instantiate(itemDestructiblePrefab, cellCenter, Quaternion.identity);
        }
    }


    public void ExplodeInDirection(Vector2 direction, int length)
    {
        Vector2 position = transform.position;
        DoExplode(position, direction, length);

        Destroy(gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        MovementController player = collision.gameObject.GetComponent<MovementController>();
        if (player != null && player.canKickBomb)
        {
            Vector2 dir = player.GetMoveDirection();

            if (dir != Vector2.zero)
            {
                moveDirection = dir.normalized;
                isMoving = true;
            }
        }
    }
}