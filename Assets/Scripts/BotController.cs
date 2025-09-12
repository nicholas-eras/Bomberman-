using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class BotController : MonoBehaviour
{
    [Header("References")]
    public Transform player;                    // Referência ao player
    public Tilemap undestructibleTiles;         // Tilemap das paredes
    
    [Header("Movement Settings")]
    public float speed = 3f;                    // Velocidade do bot (um pouco menor que o player)
    public float pathUpdateRate = 0.3f;         // Taxa de atualização do pathfinding
    
    [Header("Sprites")]
    public AnimatedSpriteRenderer spriteRendererUp;
    public AnimatedSpriteRenderer spriteRendererDown;
    public AnimatedSpriteRenderer spriteRendererLeft;
    public AnimatedSpriteRenderer spriteRendererRight;
    public AnimatedSpriteRenderer spriteRendererDeath;
    
    private Rigidbody2D rb;
    private Vector2 direction = Vector2.zero;
    private AnimatedSpriteRenderer activeSpriteRenderer;
    
    private Queue<Vector3> pathQueue = new Queue<Vector3>();
    private Vector3 currentTarget;
    private bool isMoving = false;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        activeSpriteRenderer = spriteRendererDown;
        
        // Se não definiu o player manualmente, tenta encontrar
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }
    }
    
    private void Start()
    {
        // Verifica se as referências estão configuradas
        if (player == null)
        {
            Debug.LogError("Bot: Player reference not found!");
            return;
        }
        
        if (undestructibleTiles == null)
        {
            Debug.LogError("Bot: UndestructibleTiles tilemap not assigned!");
            return;
        }
        
        // CORREÇÃO 1: Alinha o bot ao centro da célula no início
        SnapToGrid();
        
        // Debug inicial
        Vector3Int botCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3Int playerCell = undestructibleTiles.WorldToCell(player.position);
        Debug.Log($"Bot starting at cell: {botCell}, Player at cell: {playerCell}");
        
        // Inicia o pathfinding
        InvokeRepeating(nameof(UpdatePath), 0f, pathUpdateRate);
    }
    
    // CORREÇÃO 1: Método para alinhar o bot ao grid
    private void SnapToGrid()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3 cellCenter = undestructibleTiles.GetCellCenterWorld(currentCell);
        transform.position = cellCenter;
        rb.position = cellCenter;
    }
    
    private void UpdatePath()
    {
        if (player == null) return;

        List<Vector3> path = FindPath(transform.position, player.position);

        if (path != null && path.Count > 0)
        {
            pathQueue.Clear();

            for (int i = 1; i < path.Count; i++)
            {
                pathQueue.Enqueue(path[i]);
            }

            // 👉 Força o bot a seguir o novo caminho, mesmo se já estiver se movendo
            MoveToNextTarget();
        }
    }

    
    private void MoveToNextTarget()
    {
        if (pathQueue.Count > 0)
        {
            currentTarget = pathQueue.Dequeue();
            isMoving = true;
            
            // CORREÇÃO 3: Calcula direção baseada apenas nos eixos principais
            Vector3 currentPos = transform.position;
            Vector3 targetDirection = (currentTarget - currentPos).normalized;
            
            // Força movimento apenas nos eixos principais (evita movimento diagonal)
            if (Mathf.Abs(targetDirection.x) > Mathf.Abs(targetDirection.y))
            {
                direction = new Vector2(Mathf.Sign(targetDirection.x), 0);
            }
            else
            {
                direction = new Vector2(0, Mathf.Sign(targetDirection.y));
            }
            
            UpdateAnimation();
        }
        else
        {
            direction = Vector2.zero;
            SetAnimation(activeSpriteRenderer);
            isMoving = false;
        }
    }
    
    private void FixedUpdate()
    {
        if (isMoving)
        {
            Vector3 currentPos = transform.position;
            
            // Melhora detecção de chegada ao target
            float distanceToTarget = Vector3.Distance(currentPos, currentTarget);
            float moveDistance = speed * Time.fixedDeltaTime;
            
            if (distanceToTarget <= moveDistance || distanceToTarget < 0.05f)
            {
                // Posiciona exatamente no target (snap)
                rb.MovePosition(currentTarget);
                transform.position = currentTarget;
                
                // Verifica se há próximo target na fila antes de parar
                if (pathQueue.Count > 0)
                {
                    MoveToNextTarget();
                }
                else
                {
                    // Para o movimento se não há mais targets
                    direction = Vector2.zero;
                    SetAnimation(activeSpriteRenderer);
                    isMoving = false;
                }
            }
            else
            {
                // Move usando direção já calculada (apenas eixos principais)
                Vector2 newPosition = rb.position + direction * speed * Time.fixedDeltaTime;
                rb.MovePosition(newPosition);
            }
        }
    }
    
    private void UpdateAnimation()
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            Debug.Log(direction.x);
            // Movimento horizontal
            if (direction.x > 0)
                SetAnimation(spriteRendererRight);
            else
                SetAnimation(spriteRendererLeft);
        }
        else if (Mathf.Abs(direction.y) > 0.1f)
        {
            // Movimento vertical
            if (direction.y > 0)
                SetAnimation(spriteRendererUp);
            else
                SetAnimation(spriteRendererDown);
        }
    }
    
    private void SetAnimation(AnimatedSpriteRenderer spriteRenderer)
    {
        // Desabilita todos os sprites
        spriteRendererUp.enabled = false;
        spriteRendererDown.enabled = false;
        spriteRendererLeft.enabled = false;
        spriteRendererRight.enabled = false;
        
        // Abilita o sprite correto
        spriteRenderer.enabled = true;
        activeSpriteRenderer = spriteRenderer;
        activeSpriteRenderer.idle = direction == Vector2.zero;
    }
    
    // Pathfinding simples usando BFS (Breadth-First Search)
    private List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        // CORREÇÃO 5: Garante que as posições estão alinhadas ao grid
        Vector3Int startCell = undestructibleTiles.WorldToCell(startPos);
        Vector3Int targetCell = undestructibleTiles.WorldToCell(targetPos);
        
        // Se o target está na mesma célula, não precisa mover
        if (startCell == targetCell)
            return null;
        
        // BFS
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        
        queue.Enqueue(startCell);
        visited.Add(startCell);
        
        Vector3Int[] directions = {
            Vector3Int.up,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.right
        };
        
        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            
            // Chegou ao destino
            if (current == targetCell)
            {
                return ReconstructPath(cameFrom, startCell, targetCell);
            }
            
            // Explora vizinhos
            foreach (Vector3Int dir in directions)
            {
                Vector3Int neighbor = current + dir;
                
                if (!visited.Contains(neighbor) && !IsWall(neighbor))
                {
                    visited.Add(neighbor);
                    cameFrom[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }
        
        return null; // Não encontrou caminho
    }
    
    private bool IsWall(Vector3Int cellPosition)
    {
        // Verifica se há uma tile indestrutível nesta posição
        bool hasWall = undestructibleTiles.HasTile(cellPosition);
        
        // Debug adicional para verificar paredes
        if (hasWall)
        {
            Debug.Log($"Wall detected at {cellPosition}");
        }
        
        return hasWall;
    }
    
    private List<Vector3> ReconstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int start, Vector3Int target)
    {
        List<Vector3> path = new List<Vector3>();
        Vector3Int current = target;
        
        while (current != start)
        {
            // CORREÇÃO 6: Garante que todos os pontos do path estão no centro das células
            Vector3 worldPos = undestructibleTiles.GetCellCenterWorld(current);
            path.Add(worldPos);
            current = cameFrom[current];
        }
        
        // Adiciona posição inicial (também centralizada)
        Vector3 startWorldPos = undestructibleTiles.GetCellCenterWorld(start);
        path.Add(startWorldPos);
        
        path.Reverse();
        return path;
    }
    
    // CORREÇÃO 7: Método público para realinhar o bot (útil para debug)
    [ContextMenu("Snap to Grid")]
    public void ForceSnapToGrid()
    {
        SnapToGrid();
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Explosion"))
        {
            DeathSequence();
        }
    }
    
    private void DeathSequence()
    {
        this.enabled = false;
        CancelInvoke(); // Para o pathfinding
        
        spriteRendererUp.enabled = false;
        spriteRendererDown.enabled = false;
        spriteRendererLeft.enabled = false;
        spriteRendererRight.enabled = false;
        spriteRendererDeath.enabled = true;
        
        Invoke(nameof(OnDeathSequenceEnded), 1.25f);
    }
    
    private void OnDeathSequenceEnded()
    {
        gameObject.SetActive(false);
        GameManager.Instance.CheckWinState();
    }
    
    private void OnDrawGizmos()
    {
        // Visualiza o caminho no editor
        if (Application.isPlaying && pathQueue != null)
        {
            Gizmos.color = Color.red;
            Vector3 lastPos = transform.position;
            
            foreach (Vector3 point in pathQueue)
            {
                Gizmos.DrawLine(lastPos, point);
                Gizmos.DrawWireSphere(point, 0.1f);
                lastPos = point;
            }
            
            // Mostra o target atual
            if (isMoving)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentTarget, 0.2f);
            }
        }
        
        // CORREÇÃO 8: Mostra o centro da célula atual
        if (undestructibleTiles != null)
        {
            Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
            Vector3 cellCenter = undestructibleTiles.GetCellCenterWorld(currentCell);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(cellCenter, Vector3.one * 0.1f);
        }
    }
}