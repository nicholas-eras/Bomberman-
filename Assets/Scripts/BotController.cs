using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using Priority_Queue;

[RequireComponent(typeof(Rigidbody2D))]
public class BotController : MonoBehaviour
{
    [Header("References")]
    public Transform player;                    
    [Header("Tilemaps")]
    public Tilemap undestructibleTiles;
    public Tilemap destructibleTiles;
    public Tilemap scenary;
    
    [Header("Movement Settings")]
    public float speed = 3f;                    
    public float pathUpdateRate = 0.3f;         
    
    [Header("Bomb Settings")]
    public GameObject bombPrefab;
    public float bombFuseTime = 2f;
    public int bombAmount = 1;
    public Explosion explosionPrefab;
    public LayerMask explosionLayerMask;
    public float explosionDuration = 1f;
    public int explosionRadius = 2;
    public Destructible destructiblePrefab;
    public Destructible itemDestructiblePrefab;
    
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
    
    // Variáveis para movimento suave e alinhado
    private bool isMovingToTarget = false;
    private Vector3 moveStartPosition;
    private float moveProgress = 0f;
    
    // Variáveis para sistema de bombas
    private int bombsRemaining;
    private bool isPlacingBomb = false;
    private bool isFleeingFromBomb = false;
    private Vector3Int lastBombPosition;
    private float bombPlacedTime;
    
    // Lista de posições de bombas ativas para evitar
    private List<Vector3Int> activeBombPositions = new List<Vector3Int>();
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        activeSpriteRenderer = spriteRendererDown;
        bombsRemaining = bombAmount;

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        // Ignora colisão entre bot e player/bots
        int botLayer = LayerMask.NameToLayer("Player");
        int playerLayer = LayerMask.NameToLayer("Player");

        Physics2D.IgnoreLayerCollision(botLayer, botLayer, true);
        Physics2D.IgnoreLayerCollision(botLayer, playerLayer, true);
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
        
        ForceSnapToGrid();
        
        Vector3Int botCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3Int playerCell = undestructibleTiles.WorldToCell(player.position);
        Debug.Log($"Bot starting at cell: {botCell}, Player at cell: {playerCell}");
        
        InvokeRepeating(nameof(UpdatePath), 0f, pathUpdateRate);
    }
    
    private void ForceSnapToGrid()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3 cellCenter = undestructibleTiles.GetCellCenterWorld(currentCell);
        
        transform.position = cellCenter;
        rb.position = cellCenter;
        
        isMoving = false;
        isMovingToTarget = false;
        rb.velocity = Vector2.zero;
        
        Debug.Log($"Bot snapped to grid at: {cellCenter}, Cell: {currentCell}");
    }
    
    private void UpdatePath()
    {
        if (player == null || isPlacingBomb) return;
        
        // Durante a fuga, não atualiza o caminho para o player
        if (isFleeingFromBomb) 
        {
            Debug.Log("Bot está fugindo, não perseguindo player");
            return;
        }

        List<Vector3> path = FindPath(transform.position, player.position);

        if (path != null && path.Count > 0)
        {
            pathQueue.Clear();

            for (int i = 1; i < path.Count; i++)
            {
                pathQueue.Enqueue(path[i]);
            }

            if (!isMovingToTarget)
            {
                MoveToNextTarget();
            }
        }
    }
    
    private void MoveToNextTarget()
    {
        if (pathQueue.Count <= 0)
        {
            StopMovement();
            return;
        }

        currentTarget = pathQueue.Dequeue();

        Vector3 currentPos = transform.position;
        Vector3Int currentCell = undestructibleTiles.WorldToCell(currentPos);
        Vector3Int targetCell = undestructibleTiles.WorldToCell(currentTarget);

        if (currentCell == targetCell)
        {
            MoveToNextTarget();
            return;
        }

        // Verifica se o próximo movimento vai para um tile destrutível
        if (destructibleTiles != null && destructibleTiles.HasTile(targetCell) && bombsRemaining > 0)
        {
            Debug.Log($"Bot encontrou tile destrutível em {targetCell}, colocando bomba...");
            StartCoroutine(PlaceBombAndFlee(targetCell));
            return;
        }

        // Define direção com base nas células
        if (targetCell.x > currentCell.x)
        {
            direction = Vector2.right;
            currentTarget = undestructibleTiles.GetCellCenterWorld(new Vector3Int(targetCell.x, currentCell.y, 0));
        }
        else if (targetCell.x < currentCell.x)
        {
            direction = Vector2.left;
            currentTarget = undestructibleTiles.GetCellCenterWorld(new Vector3Int(targetCell.x, currentCell.y, 0));
        }
        else if (targetCell.y > currentCell.y)
        {
            direction = Vector2.up;
            currentTarget = undestructibleTiles.GetCellCenterWorld(new Vector3Int(currentCell.x, targetCell.y, 0));
        }
        else
        {
            direction = Vector2.down;
            currentTarget = undestructibleTiles.GetCellCenterWorld(new Vector3Int(currentCell.x, targetCell.y, 0));
        }

        isMovingToTarget = true;
        isMoving = true;
        moveStartPosition = currentPos;
        moveProgress = 0f;

        UpdateAnimation();

        Debug.Log($"MoveToNextTarget: cell {currentCell} -> {targetCell} | dir {direction} | targetWorld {currentTarget}");
    }

    private IEnumerator PlaceBombAndFlee(Vector3Int destructibleCell)
    {
        isPlacingBomb = true;
        StopMovement();
        
        // Coloca bomba na posição atual
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3 bombPosition = undestructibleTiles.GetCellCenterWorld(currentCell);
        
        GameObject bombObj = Instantiate(bombPrefab, bombPosition, Quaternion.identity);
        bombsRemaining--;
        lastBombPosition = currentCell;
        bombPlacedTime = Time.time;
        
        // Adiciona à lista de bombas ativas
        activeBombPositions.Add(currentCell);
        
        // Inicializa a bomba
        Bomb bomb = bombObj.GetComponent<Bomb>();
        bomb.Init(bombFuseTime, explosionRadius, explosionDuration, explosionPrefab,
                explosionLayerMask, destructibleTiles, undestructibleTiles,
                destructiblePrefab, itemDestructiblePrefab);
        
        Debug.Log($"Bot colocou bomba em {currentCell}");
        
        // Aguarda um frame para a bomba ser inicializada
        yield return null;
        
        // Inicia fuga IMEDIATAMENTE
        isPlacingBomb = false;
        isFleeingFromBomb = true;
        
        // Encontra e executa a fuga imediatamente
        Vector3Int safePosition = FindBestSafePositionSimplified();
        if (safePosition != Vector3Int.zero)
        {
            ExecuteFleeToPosition(safePosition);
        }
        else
        {
            Debug.LogWarning("Nenhuma posição segura encontrada, fuga de emergência!");
            EmergencyFlee();
        }
        
        // Monitora quando a bomba explode para remover da lista
        StartCoroutine(MonitorBombExplosion(bombObj, currentCell));
    }
    
    private void ExecuteFleeToPosition(Vector3Int safePosition)
    {
        Vector3 safeWorldPos = undestructibleTiles.GetCellCenterWorld(safePosition);
        List<Vector3> fleePath = FindSafePath(transform.position, safeWorldPos);
        
        if (fleePath != null && fleePath.Count > 0)
        {
            pathQueue.Clear();
            
            // Adiciona todos os pontos do caminho de fuga
            for (int i = 1; i < fleePath.Count; i++)
            {
                pathQueue.Enqueue(fleePath[i]);
            }
            
            Debug.Log($"Caminho de fuga encontrado com {fleePath.Count} pontos para {safePosition}");
            
            // Inicia movimento imediatamente se não estiver movendo
            if (!isMovingToTarget)
            {
                MoveToNextTarget();
            }
        }
        else
        {
            Debug.LogWarning("Não foi possível encontrar caminho de fuga!");
            // Fuga de emergência - move para qualquer direção que não seja perigosa
            EmergencyFlee();
        }
    }
    
    private void EmergencyFlee()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3Int[] emergencyDirections = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        
        foreach (Vector3Int dir in emergencyDirections)
        {
            Vector3Int testCell = currentCell + dir;
            
            if (!IsWallUndestructible(testCell) && IsSafeFromBombs(testCell))
            {
                Vector3 emergencyTarget = undestructibleTiles.GetCellCenterWorld(testCell);
                pathQueue.Clear();
                pathQueue.Enqueue(emergencyTarget);
                
                Debug.Log($"Fuga de emergência para {testCell}");
                
                if (!isMovingToTarget)
                {
                    MoveToNextTarget();
                }
                break;
            }
        }
    }
    
    private List<Vector3> FindSafePath(Vector3 startPos, Vector3 targetPos)
    {
        Vector3Int startCell = undestructibleTiles.WorldToCell(startPos);
        Vector3Int targetCell = undestructibleTiles.WorldToCell(targetPos);

        if (startCell == targetCell)
            return null;

        var frontier = new SimplePriorityQueue<Vector3Int>();
        frontier.Enqueue(startCell, 0);

        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var costSoFar = new Dictionary<Vector3Int, int>();

        cameFrom[startCell] = startCell;
        costSoFar[startCell] = 0;

        Vector3Int[] directions = {
            Vector3Int.up,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.right
        };

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();

            if (current == targetCell)
            {
                return ReconstructPath(cameFrom, startCell, targetCell);
            }

            foreach (Vector3Int dir in directions)
            {
                Vector3Int neighbor = current + dir;

                // Durante fuga, evita completamente áreas perigosas
                if (IsWallUndestructible(neighbor) || !IsSafeFromBombs(neighbor)) 
                    continue;

                int newCost = costSoFar[current] + GetSafeTileCost(neighbor);

                if (!costSoFar.ContainsKey(neighbor) || newCost < costSoFar[neighbor])
                {
                    costSoFar[neighbor] = newCost;
                    // Prioridade pela distância até o alvo + custo do tile
                    int priority = newCost + GetManhattanDistance(neighbor, targetCell);
                    frontier.Enqueue(neighbor, priority);
                    cameFrom[neighbor] = current;
                }
            }
        }

        return null; // Sem caminho seguro encontrado
    }
    
    private int GetManhattanDistance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
    
    private int GetSafeTileCost(Vector3Int cellPosition)
    {
        // Custo base
        int cost = 1;
        
        // Tiles destrutíveis têm custo maior (mas ainda passáveis durante fuga)
        if (destructibleTiles != null && destructibleTiles.HasTile(cellPosition))
            cost = 3;
            
        return cost;
    }
    
    private bool IsSafeFromBombs(Vector3Int cellPosition)
    {
        // Verifica se a posição está fora do alcance de todas as bombas ativas
        foreach (Vector3Int bombPos in activeBombPositions)
        {
            if (IsInExplosionRange(cellPosition, bombPos))
            {
                return false;
            }
        }
        return true;
    }
    
    private IEnumerator MonitorBombExplosion(GameObject bombObj, Vector3Int bombCell)
    {
        // Espera a bomba explodir
        while (bombObj != null)
            yield return null;
            
        // Remove da lista de bombas ativas
        activeBombPositions.Remove(bombCell);
        
        // Restaura bombas disponíveis
        bombsRemaining++;
        
        Debug.Log($"Bomba em {bombCell} explodiu, bot pode usar bombas novamente");
        
        // Para de fugir se não há mais bombas ativas próximas
        if (!IsInDangerZone())
        {
            isFleeingFromBomb = false;
            Debug.Log("Bot não está mais em perigo, retomando perseguição ao player");
        }
    }
    
    private Vector3Int FindBestSafePositionSimplified()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        
        // Busca mais simples - testa direções cardinais primeiro
        Vector3Int[] cardinalDirections = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        
        // Primeiro tenta se mover para fora da zona de perigo diretamente
        for (int distance = explosionRadius + 1; distance <= 3; distance++)
        {
            foreach (Vector3Int dir in cardinalDirections)
            {
                Vector3Int testCell = currentCell + (dir * distance);
                
                if (IsSafePosition(testCell))
                {
                    Debug.Log($"Posição segura direta encontrada: {testCell} na direção {dir} distância {distance}");
                    return testCell;
                }
            }
        }
        
        // Se não encontrou direção direta, faz busca em área
        for (int radius = explosionRadius + 1; radius <= 5; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    Vector3Int testCell = currentCell + new Vector3Int(x, y, 0);
                    
                    if (IsSafePosition(testCell))
                    {
                        Debug.Log($"Posição segura em área encontrada: {testCell}");
                        return testCell;
                    }
                }
            }
        }
        
        Debug.LogWarning("Nenhuma posição segura encontrada!");
        return Vector3Int.zero;
    }
    
    private bool IsSafePosition(Vector3Int cell)
    {
        // Não pode ser parede
        if (IsWallUndestructible(cell)) return false;
        
        // Deve estar seguro de todas as bombas
        if (!IsSafeFromBombs(cell)) return false;
        
        return true;
    }
    
    private bool IsInExplosionRange(Vector3Int position, Vector3Int bombPosition)
    {
        // Verifica se está na linha horizontal ou vertical da bomba
        bool sameLine = (position.x == bombPosition.x) || (position.y == bombPosition.y);
        if (!sameLine) return false;
        
        // Calcula distância Manhattan
        int distance = Mathf.Abs(position.x - bombPosition.x) + Mathf.Abs(position.y - bombPosition.y);
        return distance <= explosionRadius;
    }
    
    private bool IsInDangerZone()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        return !IsSafeFromBombs(currentCell);
    }
    
    private void StopMovement()
    {
        direction = Vector2.zero;
        isMoving = false;
        isMovingToTarget = false;
        rb.velocity = Vector2.zero;
        SetAnimation(activeSpriteRenderer);
        
        ForceSnapToGrid();
    }
    
    private void FixedUpdate()
    {
        if (isMovingToTarget)
        {
            Vector2 vel = direction * speed;

            if (Mathf.Abs(direction.x) > 0.5f) vel.y = 0f;
            else vel.x = 0f;

            rb.velocity = vel;

            Vector2 toTarget = (currentTarget - transform.position);
            float proj = Vector2.Dot(toTarget, direction);

            if (proj <= 0.01f || Vector3.Distance(transform.position, currentTarget) < 0.05f)
            {
                rb.velocity = Vector2.zero;
                transform.position = currentTarget;
                CheckNextTarget();
            }
        }
        else
        {
            rb.velocity = Vector2.zero;
        }
    }
    
    private void CheckNextTarget()
    {
        if (pathQueue.Count > 0)
        {
            MoveToNextTarget();
        }
        else
        {
            StopMovement();
        }
    }
    
    private void UpdateAnimation()
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            if (direction.x > 0)
                SetAnimation(spriteRendererRight);
            else
                SetAnimation(spriteRendererLeft);
        }
        else if (Mathf.Abs(direction.y) > 0.1f)
        {
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
        return FindPathToPosition(startPos, targetPos);
    }
    
    private List<Vector3> FindPathToPosition(Vector3 startPos, Vector3 targetPos)
    {
        Vector3Int startCell = undestructibleTiles.WorldToCell(startPos);
        Vector3Int targetCell = undestructibleTiles.WorldToCell(targetPos);

        if (startCell == targetCell)
            return null;

        var frontier = new SimplePriorityQueue<Vector3Int>();
        frontier.Enqueue(startCell, 0);

        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var costSoFar = new Dictionary<Vector3Int, int>();

        cameFrom[startCell] = startCell;
        costSoFar[startCell] = 0;

        Vector3Int[] directions = {
            Vector3Int.up,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.right
        };

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();

            if (current == targetCell)
            {
                return ReconstructPath(cameFrom, startCell, targetCell);
            }

            foreach (Vector3Int dir in directions)
            {
                Vector3Int neighbor = current + dir;

                if (IsWallUndestructible(neighbor)) continue;

                int newCost = costSoFar[current] + GetTileCost(neighbor);

                if (!costSoFar.ContainsKey(neighbor) || newCost < costSoFar[neighbor])
                {
                    costSoFar[neighbor] = newCost;
                    frontier.Enqueue(neighbor, newCost);
                    cameFrom[neighbor] = current;
                }
            }
        }

        return null;
    }

    private bool IsWallUndestructible(Vector3Int cellPosition)
    {
        return undestructibleTiles.HasTile(cellPosition);
    }

    private int GetTileCost(Vector3Int cellPosition)
    {
        if (destructibleTiles != null && destructibleTiles.HasTile(cellPosition))
            return 5;
        return 1;
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
        
        // Bombas ativas
        if (Application.isPlaying && activeBombPositions.Count > 0)
        {
            Gizmos.color = Color.red;
            foreach (Vector3Int bombPos in activeBombPositions)
            {
                Vector3 worldPos = undestructibleTiles.GetCellCenterWorld(bombPos);
                Gizmos.DrawWireSphere(worldPos, 0.3f);
                
                // Mostra área de explosão
                for (int i = 1; i <= explosionRadius; i++)
                {
                    Gizmos.DrawWireCube(worldPos + Vector3.up * i, Vector3.one * 0.1f);
                    Gizmos.DrawWireCube(worldPos + Vector3.down * i, Vector3.one * 0.1f);
                    Gizmos.DrawWireCube(worldPos + Vector3.left * i, Vector3.one * 0.1f);
                    Gizmos.DrawWireCube(worldPos + Vector3.right * i, Vector3.one * 0.1f);
                }
            }
        }
        
        if (undestructibleTiles != null)
        {
            Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
            Vector3 cellCenter = undestructibleTiles.GetCellCenterWorld(currentCell);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(cellCenter, Vector3.one * 0.1f);
            
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, cellCenter);
        }
    }
}