using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Bomb : MonoBehaviour
{
    public float fuseTime;
    public int explosionRadius;
    private float explosionDuration;
    private Explosion explosionPrefab;
    private LayerMask explosionLayerMask;
    private Tilemap destructibleTiles;
    private Destructible destructiblePrefab;

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
        Destructible destructiblePrefab
    )
    {
        this.fuseTime = fuseTime;
        this.explosionRadius = explosionRadius;
        this.explosionDuration = explosionDuration;
        this.explosionPrefab = explosionPrefab;
        this.explosionLayerMask = explosionLayerMask;
        this.destructibleTiles = destructibleTiles;
        this.destructiblePrefab = destructiblePrefab;

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
        Vector2 position = transform.position;

        // Cria explosão central - esta deve usar o sprite "start" apenas
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

        // Verifica se há bloqueio por objetos sólidos ANTES de criar a explosão
        Collider2D hit = Physics2D.OverlapBox(position, Vector2.one / 2f, 0f, explosionLayerMask);
        if (hit != null)
        {
            // Mesmo com bloqueio, tenta destruir o tile destrutível
            ClearDestructible(position);
            return; // Para a propagação aqui
        }

        // Cria a explosão visual
        Explosion explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        
        // Se é a última célula da direção, usa o sprite "end", senão usa "middle"
        if (length == 1)
        {
            explosion.SetActiveRenderer(explosion.end);
        }
        else
        {
            explosion.SetActiveRenderer(explosion.middle);
        }
        
        explosion.SetDirection(direction);
        explosion.DestroyAfter(explosionDuration);

        // Tenta destruir tiles na posição atual (mesmo sem bloqueio)
        ClearDestructible(position);

        // Próxima célula
        DoExplode(position + direction, direction, length - 1);
    }

    public void ExplodeInDirection(Vector2 direction, int length)
    {
        Vector2 position = transform.position;

        // Para o special move, não pula célula - começa direto da posição atual
        DoExplode(position, direction, length);

        Destroy(gameObject);
    }

    private void ClearDestructible(Vector2 position)
    {
        Vector3Int cell = destructibleTiles.WorldToCell(position);
        TileBase tile = destructibleTiles.GetTile(cell);

        if (tile != null)
        {
            // Pega o centro da célula em coordenadas de mundo
            Vector3 cellCenter = destructibleTiles.GetCellCenterWorld(cell);

            Instantiate(destructiblePrefab, cellCenter, Quaternion.identity);
            destructibleTiles.SetTile(cell, null);
        }
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