using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class BotController : MonoBehaviour
{
    [Header("References")]
    public Transform player;                    
    [Header("Tilemaps")]
    public Tilemap undestructibleTiles;
    public Tilemap destructibleTiles;
    [Header("Movement Settings")]
    public float speed = 3f;                    
    public float pathUpdateRate = 0.3f;         
    
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
    
    // CORREÇÃO: Variáveis para movimento suave e alinhado
    private bool isMovingToTarget = false;
    private Vector3 moveStartPosition;
    private float moveProgress = 0f;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        activeSpriteRenderer = spriteRendererDown;
        
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
        
        // CORREÇÃO: Força alinhamento completo no início
        ForceSnapToGrid();
        
        Vector3Int botCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3Int playerCell = undestructibleTiles.WorldToCell(player.position);
        Debug.Log($"Bot starting at cell: {botCell}, Player at cell: {playerCell}");
        
        InvokeRepeating(nameof(UpdatePath), 0f, pathUpdateRate);
    }
    
    // CORREÇÃO: Método melhorado para alinhamento ao grid
    private void ForceSnapToGrid()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3 cellCenter = undestructibleTiles.GetCellCenterWorld(currentCell);
        
        // Força todas as posições serem iguais
        transform.position = cellCenter;
        rb.position = cellCenter;
        
        // Para qualquer movimento em progresso
        isMoving = false;
        isMovingToTarget = false;
        rb.velocity = Vector2.zero;
        
        Debug.Log($"Bot snapped to grid at: {cellCenter}, Cell: {currentCell}");
    }
    
    private void UpdatePath()
    {
        if (player == null) return;

        List<Vector3> path = FindPath(transform.position, player.position);

        if (path != null && path.Count > 0)
        {
            pathQueue.Clear();

            // Pula o primeiro ponto (posição atual) e adiciona os outros
            for (int i = 1; i < path.Count; i++)
            {
                pathQueue.Enqueue(path[i]);
            }

            // Se não está se movendo, inicia movimento
            if (!isMovingToTarget)
            {
                MoveToNextTarget();
            }
        }
    }
    
    private void MoveToNextTarget()
    {
        if (pathQueue.Count > 0)
        {
            // Para o movimento atual se estiver em progresso
            isMovingToTarget = false;
            
            // Pega próximo target
            currentTarget = pathQueue.Dequeue();
            
            // CORREÇÃO: Calcula direção APENAS nos eixos principais
            Vector3 currentPos = transform.position;
            Vector3 difference = currentTarget - currentPos;
            
            // Força movimento apenas horizontal OU vertical (nunca diagonal)
            if (Mathf.Abs(difference.x) > Mathf.Abs(difference.y))
            {
                // Movimento horizontal
                direction = new Vector2(Mathf.Sign(difference.x), 0);
                currentTarget = new Vector3(currentTarget.x, currentPos.y, currentPos.z);
            }
            else
            {
                // Movimento vertical
                direction = new Vector2(0, Mathf.Sign(difference.y));
                currentTarget = new Vector3(currentPos.x, currentTarget.y, currentPos.z);
            }
            
            // Inicia movimento suave
            moveStartPosition = currentPos;
            moveProgress = 0f;
            isMovingToTarget = true;
            isMoving = true;
            
            UpdateAnimation();
            
            Debug.Log($"Moving from {currentPos} to {currentTarget} with direction {direction}");
        }
        else
        {
            // Não há mais targets
            StopMovement();
        }
    }
    
    private void StopMovement()
    {
        direction = Vector2.zero;
        isMoving = false;
        isMovingToTarget = false;
        rb.velocity = Vector2.zero;
        SetAnimation(activeSpriteRenderer);
        
        // CORREÇÃO: Garante que está alinhado ao parar
        ForceSnapToGrid();
    }
    
    private void FixedUpdate()
    {
        if (isMovingToTarget)
        {
            float moveDistance = speed * Time.fixedDeltaTime;
            float totalDistance = Vector3.Distance(moveStartPosition, currentTarget);
            
            if (totalDistance <= 0.01f) // Target muito próximo
            {
                rb.MovePosition(currentTarget);
                transform.position = currentTarget;
                CheckNextTarget();
                return;
            }
            
            moveProgress += moveDistance / totalDistance;
            
            if (moveProgress >= 1f)
            {
                // Chegou ao target
                moveProgress = 1f;
                Vector3 finalPosition = currentTarget;
                
                rb.MovePosition(finalPosition);
                transform.position = finalPosition;
                
                CheckNextTarget();
            }
            else
            {
                // CORREÇÃO: Movimento linear suave (sem diagonal)
                Vector3 newPosition = Vector3.Lerp(moveStartPosition, currentTarget, moveProgress);
                rb.MovePosition(newPosition);
            }
        }
    }
    
    private void CheckNextTarget()
    {
        if (pathQueue.Count > 0)
        {
            // Há mais targets na fila
            MoveToNextTarget();
        }
        else
        {
            // Acabaram os targets
            StopMovement();
        }
    }
    
    private void UpdateAnimation()
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
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
        spriteRendererUp.enabled = false;
        spriteRendererDown.enabled = false;
        spriteRendererLeft.enabled = false;
        spriteRendererRight.enabled = false;
        
        spriteRenderer.enabled = true;
        activeSpriteRenderer = spriteRenderer;
        activeSpriteRenderer.idle = direction == Vector2.zero;
    }
    
    private List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        Vector3Int startCell = undestructibleTiles.WorldToCell(startPos);
        Vector3Int targetCell = undestructibleTiles.WorldToCell(targetPos);
        
        if (startCell == targetCell)
            return null;
        
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
            
            if (current == targetCell)
            {
                return ReconstructPath(cameFrom, startCell, targetCell);
            }
            
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
        
        return null;
    }
    
    private bool IsWall(Vector3Int cellPosition)
    {
        bool hasUndestructible = undestructibleTiles.HasTile(cellPosition);
        bool hasDestructible = destructibleTiles != null && destructibleTiles.HasTile(cellPosition);
        return hasUndestructible || hasDestructible;
    }
    
    private List<Vector3> ReconstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int start, Vector3Int target)
    {
        List<Vector3> path = new List<Vector3>();
        Vector3Int current = target;
        
        while (current != start)
        {
            Vector3 worldPos = undestructibleTiles.GetCellCenterWorld(current);
            path.Add(worldPos);
            current = cameFrom[current];
        }
        
        Vector3 startWorldPos = undestructibleTiles.GetCellCenterWorld(start);
        path.Add(startWorldPos);
        
        path.Reverse();
        
        // Debug do caminho
        Debug.Log($"Path found with {path.Count} points:");
        for (int i = 0; i < path.Count; i++)
        {
            Debug.Log($"  Point {i}: {path[i]}");
        }
        
        return path;
    }
    
    [ContextMenu("Snap to Grid")]
    public void ForceSnapToGridMenu()
    {
        ForceSnapToGrid();
    }
    
    [ContextMenu("Debug Position")]
    public void DebugPosition()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3 cellCenter = undestructibleTiles.GetCellCenterWorld(currentCell);
        
        Debug.Log($"Transform Position: {transform.position}");
        Debug.Log($"RigidBody Position: {rb.position}");
        Debug.Log($"Current Cell: {currentCell}");
        Debug.Log($"Cell Center: {cellCenter}");
        Debug.Log($"Distance to center: {Vector3.Distance(transform.position, cellCenter)}");
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
        CancelInvoke();
        
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
        // Caminho
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
            
            if (isMovingToTarget)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentTarget, 0.2f);
                Gizmos.DrawLine(transform.position, currentTarget);
            }
        }
        
        // Centro da célula atual
        if (undestructibleTiles != null)
        {
            Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
            Vector3 cellCenter = undestructibleTiles.GetCellCenterWorld(currentCell);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(cellCenter, Vector3.one * 0.1f);
            
            // NOVO: Mostra a diferença entre posição real e centro da célula
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, cellCenter);
        }
    }
}