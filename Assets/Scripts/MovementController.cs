using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
public class MovementController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 direction = Vector2.down;
    public float speed = 5f;
    public bool canKickBomb = false;

    [Header("Input")]
    public KeyCode inputUp = KeyCode.W;
    public KeyCode inputDown = KeyCode.S;
    public KeyCode inputLeft = KeyCode.A;
    public KeyCode inputRight = KeyCode.D;
    public KeyCode InputPlaceBomb = KeyCode.Space;
    public KeyCode inputSpecialMove = KeyCode.LeftShift;

    [Header("Sprites")]
    public AnimatedSpriteRenderer spriteRendererUp;
    public AnimatedSpriteRenderer spriteRendererDown;
    public AnimatedSpriteRenderer spriteRendererLeft;
    public AnimatedSpriteRenderer spriteRendererRight;
    public AnimatedSpriteRenderer spriteRendererDeath;
    public AnimatedSpriteRenderer spriteSpecialMove;
    public AnimatedSpriteRenderer spriteSpecialMoveUp;
    public AnimatedSpriteRenderer spriteSpecialMoveDown;
    public AnimatedSpriteRenderer spriteSpecialMoveRight;
    public AnimatedSpriteRenderer spriteSpecialMoveLeft;

    private AnimatedSpriteRenderer activeSpriteRenderer;

    [Header("Special Move Settings")]
    public GameObject explosionPrefabGO;        // arraste o prefab do Explosion aqui no Inspector
    public Tilemap destructibleTiles;           // se quiser que a rajada destrua blocos
    public Tilemap undestructibleTiles;
    public Destructible destructiblePrefab;     // se tiver prefabs destrutíveis
    public Destructible itemDestructiblePrefab;
    public float explosionDuration = 0.5f; 
    public int explosionDistance = 9;
    [SerializeField] private LayerMask explosionLayerMask;

    private Explosion explosionPrefab;          // componente Explosion que vamos usar
 
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        activeSpriteRenderer = spriteRendererDown;

        if (explosionPrefabGO != null)
        {
            explosionPrefab = explosionPrefabGO.GetComponent<Explosion>();
        }
    }

    private void Update()
    {
        // --- SPECIAL MOVE ---
        if (Input.GetKey(this.inputSpecialMove))
        {
            if (Input.GetKeyDown(this.inputRight))
            {
                this.SetAnimation(this.spriteSpecialMoveRight);
                SpawnExplosion(Vector2.right, true);
            }
            else if (Input.GetKeyDown(this.inputLeft))
            {
                this.SetAnimation(this.spriteSpecialMoveLeft); 
                SpawnExplosion(Vector2.left, true);
            }
            else if (Input.GetKeyDown(this.inputUp))
            {
                this.SetAnimation(this.spriteSpecialMoveUp); 
                SpawnExplosion(Vector2.up, true);
            }
            else if (Input.GetKeyDown(this.inputDown))
            {
                this.SetAnimation(this.spriteSpecialMoveDown); 
                SpawnExplosion(Vector2.down, true);
            }
            else
            {
                this.SetAnimation(this.spriteSpecialMove); 
            }
        }

        // --- MOVIMENTO NORMAL ---
        else if (Input.GetKey(this.inputUp))
        {
            this.SetDirection(Vector2.up, this.spriteRendererUp);
        }
        else if (Input.GetKey(this.inputDown))
        {
            this.SetDirection(Vector2.down, this.spriteRendererDown);
        }
        else if (Input.GetKey(this.inputLeft))
        {
            this.SetDirection(Vector2.left, this.spriteRendererLeft);
        }
        else if (Input.GetKey(this.inputRight))
        {
            this.SetDirection(Vector2.right, this.spriteRendererRight);
        }
        else
        {
            this.SetDirection(Vector2.zero, this.activeSpriteRenderer);
        }
    }


    private void FixedUpdate()
    {
        Vector2 position = this.rb.position;
        Vector2 translation = this.speed * Time.fixedDeltaTime * this.direction;

        this.rb.MovePosition(position + translation);
    }

    private void SetDirection(Vector2 newDirection, AnimatedSpriteRenderer spriteRenderer)
    {
        this.direction = newDirection;
        this.SetAnimation(spriteRenderer);
    }

    private void SetAnimation(AnimatedSpriteRenderer spriteRenderer)
    {
        this.spriteRendererUp.enabled = spriteRenderer == this.spriteRendererUp;
        this.spriteRendererDown.enabled = spriteRenderer == this.spriteRendererDown;
        this.spriteRendererLeft.enabled = spriteRenderer == this.spriteRendererLeft;
        this.spriteRendererRight.enabled = spriteRenderer == this.spriteRendererRight;
        this.spriteSpecialMove.enabled = spriteRenderer == this.spriteSpecialMove;
        this.spriteSpecialMoveUp.enabled = spriteRenderer == this.spriteSpecialMoveUp;
        this.spriteSpecialMoveDown.enabled = spriteRenderer == this.spriteSpecialMoveDown;
        this.spriteSpecialMoveRight.enabled = spriteRenderer == this.spriteSpecialMoveRight;
        this.spriteSpecialMoveLeft.enabled = spriteRenderer == this.spriteSpecialMoveLeft;

        this.activeSpriteRenderer = spriteRenderer;
        this.activeSpriteRenderer.idle = this.direction == Vector2.zero;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            this.DeathSequence();
        }
    }

    private void DeathSequence()
    {
        this.enabled = false;
        // this.GetComponent<BombController>().enabled = false;

        this.spriteRendererUp.enabled = false;
        this.spriteRendererDown.enabled = false;
        this.spriteRendererLeft.enabled = false;
        this.spriteRendererRight.enabled = false;
        this.spriteRendererDeath.enabled = true;

        this.Invoke(nameof(this.OnDeathSequenceEnded), 1.25f);
    }

    private void OnDeathSequenceEnded()
    {
        this.gameObject.SetActive(false);
        GameManager.Instance.CheckWinState();
    }

    public Vector2 GetMoveDirection()
    {
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }
    
    private void SpawnExplosion(Vector2 direction, bool forceSpawn = false)
    {
        if (explosionPrefab == null) return;

        // Centraliza na célula do player
        Vector3 startPos = rb.position;
        if (destructibleTiles != null)
        {
            Vector3Int cell = destructibleTiles.WorldToCell(rb.position);
            startPos = destructibleTiles.GetCellCenterWorld(cell);
        }

        // Cria a rajada começando uma célula à frente
        Vector3 explosionStart = startPos + (Vector3)direction;

        GameObject bombObj = new GameObject("SpecialExplosion");
        bombObj.transform.position = explosionStart;

        Bomb bomb = bombObj.AddComponent<Bomb>();
        bomb.Init(
            0f,                      // fuseTime
            this.explosionDistance,                       // explosionRadius
            explosionDuration,        // explosionDuration
            explosionPrefab,          // componente Explosion
            explosionLayerMask,
            destructibleTiles,
            undestructibleTiles,
            destructiblePrefab,
            itemDestructiblePrefab
        );

        // Rajada só na direção
        bomb.ExplodeInDirection(direction, bomb.explosionRadius);
    }
}
