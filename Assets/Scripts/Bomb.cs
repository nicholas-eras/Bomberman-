using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Bomb : MonoBehaviour
{
    public float fuseTime;
    public int explosionRadius;
    private float explosionDuration;
    
    // === NOVO SISTEMA DE TEMPO RESTANTE ===
    [Header("Timing Info")]
    [SerializeField] private float remainingTime; // Visível no Inspector para debug
    public float RemainingTime => remainingTime; // Property pública para outros scripts
    public bool IsExploded { get; private set; } = false;
    public bool IsExploding { get; private set; } = false;
    
    private float plantTime; // Quando foi plantada
    
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
    public GameObject owner; // Quem colocou a bomba
    [HideInInspector] public bool isKicked = false;

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

        // === INICIALIZA SISTEMA DE TIMING ===
        plantTime = Time.time;
        remainingTime = fuseTime;
        IsExploded = false;
        IsExploding = false;

        fuseCoroutine = ExplodeAfterDelay();
        StartCoroutine(fuseCoroutine);
    }

    private void Update()
    {
        // === ATUALIZA TEMPO RESTANTE ===
        if (!IsExploded && !IsExploding)
        {
            remainingTime = fuseTime - (Time.time - plantTime);
            remainingTime = Mathf.Max(0f, remainingTime); // Não pode ser negativo
        }
        
        // Movimento da bomba (se kickada)
        if (isMoving)
        {
            Vector2 nextPos = rb.position + moveDirection * moveSpeed * Time.deltaTime;

            Collider2D hit = Physics2D.OverlapBox(nextPos, Vector2.one * 0.8f, 0f, explosionLayerMask);
            if (hit != null)
            {
                isMoving = false;
                rb.velocity = Vector2.zero;
                isKicked = false; // Permite que seja chutada novamente
                return;
            }

            rb.MovePosition(nextPos);
        }
    }

    public void Kick(Vector2 direction)
    {
        if (isKicked) return;

        isKicked = true;
        moveDirection = direction.normalized;
        isMoving = true;
        // Reinicia o timer do fusível ao ser chutada (opcional, mas adiciona dinâmica)
        // plantTime = Time.time; 
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Se uma explosão encostar na bomba, ela detona antes do tempo
        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            if (!IsExploded && !IsExploding)
            {
                StopCoroutine(fuseCoroutine);
                Explode();
            }
        }
    }

    private IEnumerator ExplodeAfterDelay()
    {
        yield return new WaitForSeconds(fuseTime);
        
        if (!IsExploded) // Só explode se não foi detonada antes
        {
            Explode();
        }
    }
    
    private void Explode()
    {
        if (IsExploded || IsExploding) return; // Evita dupla explosão
        
        IsExploding = true;
        remainingTime = 0f;
        
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

        // === MARCA COMO EXPLODIDA ANTES DE DESTRUIR ===
        IsExploded = true;
        
        // Destrói após a duração da explosão para dar tempo do bot detectar
        StartCoroutine(DestroyAfterExplosion());
    }
    
    private IEnumerator DestroyAfterExplosion()
    {
        yield return new WaitForSeconds(explosionDuration);
        Destroy(gameObject);
    }

    // === MÉTODOS ÚTEIS PARA O BOT ===
    public float GetRemainingTimePercentage()
    {
        return remainingTime / fuseTime;
    }
    
    public bool IsAboutToExplode(float warningTime = 1f)
    {
        return remainingTime <= warningTime;
    }
    
    public bool IsDangerous(Vector3Int position, Tilemap tilemap)
    {
        Vector3Int bombCell = tilemap.WorldToCell(transform.position);
        int distance = Mathf.Abs(position.x - bombCell.x) + Mathf.Abs(position.y - bombCell.y);
        return distance <= explosionRadius;
    }

    // Resto do código permanece igual...
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

        IsExploded = true;
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