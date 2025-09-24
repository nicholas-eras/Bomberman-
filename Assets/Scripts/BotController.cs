using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using Priority_Queue;

[RequireComponent(typeof(Rigidbody2D))]
public class BotController : MonoBehaviour
{
    [Header("References")]
    public List<Transform> potentialTargets = new List<Transform>(); // Lista de possíveis alvos
    public float targetSwitchInterval = 3f; // Muda de alvo a cada X segundos
    public bool targetRandomly = true; // Se true, escolhe aleatório. Se false, escolhe o mais próximo

    // === 2. ADICIONAR VARIÁVEIS PRIVADAS ===
    private Transform currentTarget; // Alvo atual que o bot está perseguindo
    private float lastTargetSwitchTime = 0f;
    private Vector3 currentMoveTarget; // Para armazenar posição de movimento atual


    [Header("Tilemaps")]
    public Tilemap undestructibleTiles;
    public Tilemap destructibleTiles;
    public Tilemap scenary;

    [Header("Movement Settings")]
    public float speed = 3f;
    public float pathUpdateRate = 0.3f;
    public bool canKickBomb = false;

    [Header("Bomb Settings")]
    public GameObject bombPrefab;
    public float bombFuseTime = 2f;
    public int bombAmount = 1;
    public Explosion explosionPrefab;
    public LayerMask explosionLayerMask;
    public float explosionDuration = 1f;
    public int explosionRadius = 1;
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
    private readonly float BotRadiusSearch = 4f;

    // Lista de posições de bombas ativas para evitar
    private List<Vector3Int> activeBombPositions = new List<Vector3Int>();

    private Coroutine fleeTimerCoroutine;


    // === 1. ADICIONAR NO HEADER (após Bomb Settings) ===
    [Header("Special Move Settings")]
    public GameObject explosionPrefabGO;        // prefab da explosão especial
    public float specialMoveCooldown = 8f;      // cooldown entre especiais
    public int specialExplosionDistance = 9;    // alcance do especial
    public float specialExplosionDuration = 0.5f;
    public float specialMoveChance = 0.3f;      // 30% chance de usar quando possível
    public float playerDistanceForSpecial = 5f; // distância mínima do player para usar

    // === 2. ADICIONAR SPRITES ESPECIAIS (após sprites normais) ===
    // Sprites do special move
    public AnimatedSpriteRenderer spriteSpecialMove;
    public AnimatedSpriteRenderer spriteSpecialMoveUp;
    public AnimatedSpriteRenderer spriteSpecialMoveDown;
    public AnimatedSpriteRenderer spriteSpecialMoveRight;
    public AnimatedSpriteRenderer spriteSpecialMoveLeft;

    // === 3. ADICIONAR VARIÁVEIS PRIVADAS (após activeBombPositions) ===
    // === VARIÁVEIS DO SPECIAL MOVE ===
    private Explosion specialExplosionPrefab;
    private float lastSpecialMoveTime = -999f;
    private bool isPerformingSpecialMove = false;
    private float specialMoveDecisionTimer = 0f;
    private readonly float specialDecisionInterval = 2f; // avalia usar special a cada 2s


    // === FUNÇÃO DE TIMER DE FUGA ===
    private IEnumerator FleeTimer(float fleeTime)
    {

        yield return new WaitForSeconds(fleeTime);

        StopFleeingFromBomb();
    }

    // === FUNÇÃO PARA PARAR A FUGA ===
    private void StopFleeingFromBomb()
    {
        isFleeingFromBomb = false;

        // Para o timer se ainda estiver rodando
        if (fleeTimerCoroutine != null)
        {
            StopCoroutine(fleeTimerCoroutine);
            fleeTimerCoroutine = null;
        }

        // Limpa qualquer movimento de fuga pendente
        if (pathQueue.Count > 0)
        {
            pathQueue.Clear();
            StopMovement();
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        activeSpriteRenderer = spriteRendererDown;
        bombsRemaining = bombAmount;
        if (explosionPrefabGO != null)
        {
            specialExplosionPrefab = explosionPrefabGO.GetComponent<Explosion>();
        }

        if (potentialTargets.Count == 0)
        {
            // Busca automaticamente objetos com tag "Player"
            GameObject[] playerObjs = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject obj in playerObjs)
            {
                if (obj.transform != this.transform) // Não adiciona a si mesmo
                {
                    potentialTargets.Add(obj.transform);
                }
            }
        }

        ChooseNewTarget();

        // Ignora colisão entre bot e player/bots
        int botLayer = LayerMask.NameToLayer("Player");
        int playerLayer = LayerMask.NameToLayer("Player");

        Physics2D.IgnoreLayerCollision(botLayer, botLayer, true);
        Physics2D.IgnoreLayerCollision(botLayer, playerLayer, true);
    }

    private void Start()
    {
        if (currentTarget == null || potentialTargets.Count == 0)
        {
            return;
        }

        if (undestructibleTiles == null)
        {
            return;
        }

        ForceSnapToGrid();

        Vector3Int botCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3Int playerCell = undestructibleTiles.WorldToCell(currentTarget.position);

        InvokeRepeating(nameof(UpdatePath), 0f, pathUpdateRate);

        lastSpecialMoveTime = -Random.Range(0f, specialMoveCooldown);
    }

    private void ForceSnapToGrid()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3 centeredPosition = undestructibleTiles.GetCellCenterWorld(currentCell);

        // Se o bot não está perfeitamente centralizado, corrige
        float distanceFromCenter = Vector3.Distance(transform.position, centeredPosition);
        if (distanceFromCenter > 0.1f)
        {
            transform.position = centeredPosition;
        }
    }
// === 8. FUNÇÕES AUXILIARES OPCIONAIS ===
[ContextMenu("Choose Random Target")]
public void ChooseRandomTarget()
{
    bool oldMode = targetRandomly;
    targetRandomly = true;
    ChooseNewTarget();
    targetRandomly = oldMode;
}

[ContextMenu("Choose Closest Target")]
public void ChooseClosestTarget()
{
    bool oldMode = targetRandomly;
    targetRandomly = false;
    ChooseNewTarget();
    targetRandomly = oldMode;
}

public Transform GetCurrentTarget()
{
    return currentTarget;
}

public int GetTargetCount()
{
    potentialTargets.RemoveAll(target => target == null);
    return potentialTargets.Count;
}
    private void UpdatePath()
    {
        if (currentTarget  == null || isPlacingBomb || isPerformingSpecialMove) return;

        // Durante a fuga, não atualiza o caminho para o player
        if (isFleeingFromBomb)
        {
            return;
        }

        if (!isFleeingFromBomb)
        {
            CheckPreciseBombDanger();
        }

        List<Vector3> path = FindPath(transform.position, currentTarget.position);

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

        // === GARANTIR QUE O BOT ESTÁ NO CENTRO DA CÉLULA ===
        ForceSnapToGrid();

        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);

        Vector3 nextTarget = pathQueue.Peek();
        Vector3Int nextTargetCell = undestructibleTiles.WorldToCell(nextTarget);

        if (currentCell == nextTargetCell)
        {
            pathQueue.Dequeue();
            MoveToNextTarget();
            return;
        }

        // Calcula diferença entre células
        Vector3Int deltaCell = nextTargetCell - currentCell;
        int manhattanDistance = Mathf.Abs(deltaCell.x) + Mathf.Abs(deltaCell.y);

        if (manhattanDistance != 1)
        {
            pathQueue.Dequeue();
            MoveToNextTarget();
            return;
        }

        // Remove da fila agora que validamos
        currentMoveTarget = pathQueue.Dequeue();

        // Verifica bomba em tile destrutível
        if (destructibleTiles != null && destructibleTiles.HasTile(nextTargetCell) && bombsRemaining > 0)
        {
            StartCoroutine(PlaceBombAndFlee(nextTargetCell));
            return;
        }

        // Define direção baseada na diferença entre células
        if (deltaCell.x > 0)
        {
            direction = Vector2.right;
        }
        else if (deltaCell.x < 0)
        {
            direction = Vector2.left;
        }
        else if (deltaCell.y > 0)
        {
            direction = Vector2.up;
        }
        else if (deltaCell.y < 0)
        {
            direction = Vector2.down;
        }

        // === FUNDAMENTAL: USA O CENTRO CORRETO COMO TARGET ===
        currentMoveTarget = undestructibleTiles.GetCellCenterWorld(nextTargetCell);

        isMovingToTarget = true;
        isMoving = true;
        moveStartPosition = transform.position;
        moveProgress = 0f;

        UpdateAnimation();
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

        // Para de fugir se não há mais bombas ativas próximas
        if (!IsInDangerZone())
        {
            isFleeingFromBomb = false;
        }
    }

    private bool IsInDangerZone()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        return !IsSafeFromBombsWithRadius(currentCell);
    }

    private void StopMovement()
    {
        direction = Vector2.zero;
        isMoving = false;
        isMovingToTarget = false;
        moveProgress = 0f;
        rb.velocity = Vector2.zero;
        SetAnimation(activeSpriteRenderer);

        ForceSnapToGrid();
    }

    private void Update()
    {
        // Avalia se deve usar special move
        specialMoveDecisionTimer += Time.deltaTime;
        if (specialMoveDecisionTimer >= specialDecisionInterval)
        {
            specialMoveDecisionTimer = 0f;
            EvaluateSpecialMoveUsage();
        }
        if (Time.time - lastTargetSwitchTime >= targetSwitchInterval)
        {
            ChooseNewTarget();
            lastTargetSwitchTime = Time.time;
        }
    }

    private void ChooseNewTarget()
{
    // Remove alvos nulos da lista
    potentialTargets.RemoveAll(target => target == null);

    if (potentialTargets.Count == 0)
    {
        currentTarget = null;
        return;
    }

    if (targetRandomly)
    {
        // Escolhe alvo aleatório
        int randomIndex = Random.Range(0, potentialTargets.Count);
        currentTarget = potentialTargets[randomIndex];
    }
    else
    {
        // Escolhe o alvo mais próximo
        Transform closestTarget = null;
        float closestDistance = float.MaxValue;

        foreach (Transform target in potentialTargets)
        {
            if (target == null) continue;

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = target;
            }
        }

        currentTarget = closestTarget;
    }

    lastTargetSwitchTime = Time.time;
}

public void AddTarget(Transform newTarget)
{
    if (newTarget != null && !potentialTargets.Contains(newTarget) && newTarget != this.transform)
    {
        potentialTargets.Add(newTarget);
    }
}

public void RemoveTarget(Transform targetToRemove)
{
    potentialTargets.Remove(targetToRemove);
    
    // Se removeu o alvo atual, escolhe um novo
    if (currentTarget == targetToRemove)
    {
        ChooseNewTarget();
    }
}

    private void EvaluateSpecialMoveUsage()
    {
        // Não usa special se já está performando um, fugindo, ou colocando bomba
        if (isPerformingSpecialMove || isFleeingFromBomb || isPlacingBomb)
            return;

        // Verifica cooldown
        if (Time.time - lastSpecialMoveTime < specialMoveCooldown)
            return;

        // Verifica se o player está próximo o suficiente
        if (currentTarget  == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, currentTarget.position);
        if (distanceToPlayer > playerDistanceForSpecial)
            return;

        // Chance aleatória de usar
        if (Random.Range(0f, 1f) > specialMoveChance)
            return;

        // Encontra a melhor direção para o special
        Vector2 bestDirection = FindBestSpecialDirection();
        if (bestDirection != Vector2.zero)
        {
            StartCoroutine(PerformSpecialMove(bestDirection));
        }
    }

    private Vector2 FindBestSpecialDirection()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        
        Vector2 bestDirection = Vector2.zero;
        int bestScore = 0;

        foreach (Vector2 dir in directions)
        {
            int score = EvaluateSpecialDirection(currentCell, dir);
            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = dir;
            }
        }

        // Só usa se tiver uma pontuação mínima (pelo menos 2 pontos)
        return bestScore >= 2 ? bestDirection : Vector2.zero;
    }

    private int EvaluateSpecialDirection(Vector3Int startCell, Vector2 direction)
    {
        int score = 0;
        Vector3Int currentCell = startCell;
        Vector3Int dirInt = new Vector3Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y), 0);

        for (int i = 1; i <= specialExplosionDistance; i++)
        {
            currentCell += dirInt;

            // Para se encontrar parede indestrutível
            if (undestructibleTiles.HasTile(currentCell))
                break;

            // Pontos por destruir blocos destrutíveis
            if (destructibleTiles.HasTile(currentCell))
            {
                score += 3;
                break; // Explosão para no primeiro bloco destrutível
            }

            // Pontos se o player estiver nessa direção (mas não muito próximo)
            if (currentTarget != null)
            {
                Vector3Int playerCell = undestructibleTiles.WorldToCell(currentTarget.position);
                if (currentCell == playerCell && i > 2) // Pelo menos 2 células de distância
                {
                    score += 5;
                }
            }

            // Pontos por alcance (quanto mais longe, melhor, mas diminui com distância)
            score += Mathf.Max(0, 2 - (i / 3));
        }

        return score;
    }

    private IEnumerator PerformSpecialMove(Vector2 direction)
    {
        isPerformingSpecialMove = true;
        lastSpecialMoveTime = Time.time;

        // Para o movimento atual
        StopMovement();

        // Escolhe a animação correta baseada na direção
        AnimatedSpriteRenderer specialSprite = GetSpecialSpriteForDirection(direction);
        
        // Ativa animação especial
        SetAnimation(specialSprite, true);

        // Aguarda o tempo de carregamento (mesmo do player)
        yield return new WaitForSeconds(0.4f);

        // Executa a explosão
        SpawnSpecialExplosion(direction);

        // Aguarda um pouco após a explosão
        yield return new WaitForSeconds(0.3f);

        // Volta ao estado normal
        isPerformingSpecialMove = false;
        SetAnimation(activeSpriteRenderer);
    }

    private AnimatedSpriteRenderer GetSpecialSpriteForDirection(Vector2 direction)
    {
        if (direction == Vector2.up) return spriteSpecialMoveUp;
        if (direction == Vector2.down) return spriteSpecialMoveDown;
        if (direction == Vector2.left) return spriteSpecialMoveLeft;
        if (direction == Vector2.right) return spriteSpecialMoveRight;
        return spriteSpecialMove; // fallback
    }

    private void SpawnSpecialExplosion(Vector2 direction)
    {
        if (specialExplosionPrefab == null) return;

        // Centraliza na célula do bot
        Vector3 startPos = rb.position;
        if (destructibleTiles != null)
        {
            Vector3Int cell = destructibleTiles.WorldToCell(rb.position);
            startPos = destructibleTiles.GetCellCenterWorld(cell);
        }

        // Cria a rajada começando uma célula à frente
        Vector3 explosionStart = startPos + (Vector3)direction;

        GameObject bombObj = new GameObject("BotSpecialExplosion");
        bombObj.transform.position = explosionStart;

        Bomb bomb = bombObj.AddComponent<Bomb>();
        bomb.Init(
            0f,                           // fuseTime (explode imediatamente)
            specialExplosionDistance,     // explosionRadius
            specialExplosionDuration,     // explosionDuration
            specialExplosionPrefab,       // componente Explosion
            explosionLayerMask,
            destructibleTiles,
            undestructibleTiles,
            destructiblePrefab,
            itemDestructiblePrefab
        );

        // Rajada só na direção especificada
        bomb.ExplodeInDirection(direction, bomb.explosionRadius);
    }

    [ContextMenu("Force Special Move")]
    public void ForceSpecialMove()
    {
        Vector2 bestDir = FindBestSpecialDirection();
        if (bestDir != Vector2.zero)
        {
            StartCoroutine(PerformSpecialMove(bestDir));
        }
    }

    void FixedUpdate()
    {
        if (!isMoving || isPerformingSpecialMove) return;

        if (isMovingToTarget)
        {
            moveProgress += speed * Time.fixedDeltaTime;

            if (moveProgress >= 1f)
            {
                // Chegou no target
                moveProgress = 1f;                
                transform.position = currentMoveTarget;
                isMovingToTarget = false;

                // Verifica próximo target
                CheckNextTarget();
            }
            else
            {
                // Ainda movendo
                transform.position = Vector3.Lerp(moveStartPosition, currentMoveTarget, moveProgress);
            }
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

    private void SetAnimation(AnimatedSpriteRenderer spriteRenderer, bool isSpecial = false)
    {
        spriteRendererUp.enabled = false;
        spriteRendererDown.enabled = false;
        spriteRendererLeft.enabled = false;
        spriteRendererRight.enabled = false;

        // Desabilita todos os sprites especiais
        if (spriteSpecialMove != null) spriteSpecialMove.enabled = false;
        if (spriteSpecialMoveUp != null) spriteSpecialMoveUp.enabled = false;
        if (spriteSpecialMoveDown != null) spriteSpecialMoveDown.enabled = false;
        if (spriteSpecialMoveRight != null) spriteSpecialMoveRight.enabled = false;
        if (spriteSpecialMoveLeft != null) spriteSpecialMoveLeft.enabled = false;

        // Ativa o sprite correto
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            activeSpriteRenderer = spriteRenderer;
            activeSpriteRenderer.idle = direction == Vector2.zero;
            
            if (isSpecial)
            {
                activeSpriteRenderer.idle = false;
                activeSpriteRenderer.RestartAnimation();
            }
        }

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
                    int priority = newCost + ManhattanDistance(neighbor, targetCell);
                    frontier.Enqueue(neighbor, priority);                    
                    cameFrom[neighbor] = current;
                }
            }
        }

        return null;
    }

    private int ManhattanDistance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private bool IsWallUndestructible(Vector3Int cellPosition)
    {
        return undestructibleTiles.HasTile(cellPosition);
    }

    private int GetTileCost(Vector3Int cellPosition)
    {
        // Tiles destrutíveis custam mais, mas ainda são passáveis
        if (destructibleTiles != null && destructibleTiles.HasTile(cellPosition))
            return 10;
        return 1;
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
                // CORREÇÃO LINHA 860: Usar currentMoveTarget
                Gizmos.DrawWireSphere(currentMoveTarget, 0.2f);
                // CORREÇÃO LINHA 861: Usar currentMoveTarget
                Gizmos.DrawLine(transform.position, currentMoveTarget);
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

            // === NOVA PARTE: DEBUG VISUAL DOS ARREDORES ===
            DrawSurroundingsGizmos(currentCell);
        }

   if (currentTarget != null)
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, currentTarget.position);
        Gizmos.DrawWireSphere(currentTarget.position, 0.5f);
        
#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.cyan;
        Vector3 midPoint = (transform.position + currentTarget.position) * 0.5f;
        UnityEditor.Handles.Label(midPoint, "CURRENT TARGET");
#endif
    }
    
    // Desenha todos os alvos potenciais
    Gizmos.color = Color.yellow;
    foreach (Transform target in potentialTargets)
    {
        if (target != null && target != currentTarget)
        {
            Gizmos.DrawWireSphere(target.position, 0.3f);
        }
    }
    }

    // Nova função para desenhar bombas em uma célula específica
    private void DrawBombsInCell(Vector3Int cellPosition, Vector3 worldPos)
    {
        // Encontra todas as bombas no jogo e desenha seus raios
        Bomb[] allBombs = FindObjectsOfType<Bomb>();

        foreach (Bomb bomb in allBombs)
        {
            Vector3Int bombCellPos = undestructibleTiles.WorldToCell(bomb.transform.position);

            // Se esta é a célula da bomba
            if (cellPosition == bombCellPos)
            {
                // Desenha um X vermelho grande para indicar bomba
                Gizmos.color = Color.red;
                float size = 0.4f;

                // Desenha X com linhas
                Gizmos.DrawLine(worldPos + new Vector3(-size / 2, -size / 2, 0), worldPos + new Vector3(size / 2, size / 2, 0));
                Gizmos.DrawLine(worldPos + new Vector3(-size / 2, size / 2, 0), worldPos + new Vector3(size / 2, -size / 2, 0));

                // Desenha círculo ao redor para destacar mais
                Gizmos.DrawWireSphere(worldPos, size * 0.7f);

#if UNITY_EDITOR
                // Adiciona texto com info da bomba
                UnityEditor.Handles.color = Color.red;
                UnityEditor.Handles.Label(worldPos + Vector3.up * 0.5f, $"BOMB R{bomb.explosionRadius}");
#endif
            }
            else
            {
                // Verifica se esta célula está na área de explosão desta bomba
                if (IsInExplosionPath(cellPosition, bombCellPos, bomb.explosionRadius))
                {
                    // Desenha linha mostrando direção da explosão
                    Vector3 bombWorldPos = undestructibleTiles.GetCellCenterWorld(bombCellPos);

                    Gizmos.color = Color.red * 0.7f; // Vermelho mais transparente
                    Gizmos.DrawLine(bombWorldPos, worldPos);

                    // Desenha pequena seta indicando direção
                    Vector3 direction = (worldPos - bombWorldPos).normalized;
                    Vector3 arrowHead = worldPos - direction * 0.1f;
                    Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0); // Perpendicular em 2D
                    Gizmos.DrawLine(worldPos, arrowHead + perpendicular * 0.05f);
                    Gizmos.DrawLine(worldPos, arrowHead - perpendicular * 0.05f);

#if UNITY_EDITOR
                    // Mostra distância da bomba
                    int distance = Mathf.RoundToInt(Vector3.Distance(bombWorldPos, worldPos));
                    UnityEditor.Handles.color = Color.red * 0.8f;
                    UnityEditor.Handles.Label(worldPos + Vector3.right * 0.3f, $"D{distance}");
#endif
                }
            }
        }
    }

    private Color GetGizmoColorForTile(Vector3Int cellPosition)
    {
        // === NOVA PRIORIDADE: BOMBA EM PRIMEIRO LUGAR ===
        // Se há bomba ativa nesta posição, sempre mostra como magenta
        if (activeBombPositions.Contains(cellPosition))
        {
            return Color.magenta; // MAGENTA = TEM BOMBA
        }

        // === PRIORIDADE ALTA: PAREDES (INDESTRUTÍVEL E DESTRUTÍVEL) ===
        // Parede indestrutível (prioridade máxima após bomba)
        if (undestructibleTiles != null && undestructibleTiles.HasTile(cellPosition))
        {
            return Color.black; // PRETO = INDESTRUTÍVEL
        }

        // Parede destrutível (segunda prioridade)
        if (destructibleTiles != null && destructibleTiles.HasTile(cellPosition))
        {
            return Color.yellow; // AMARELO = DESTRUTÍVEL
        }

        // === PRIORIDADE MÉDIA: PERIGO DE EXPLOSÃO ===
        // Zona de perigo baseada no raio real das bombas
        if (!IsSafeFromBombsWithRadius(cellPosition))
        {
            return Color.red; // VERMELHO = PERIGO
        }

        // === PRIORIDADE BAIXA: OUTROS TILES ===
        // Cenário
        if (scenary != null && scenary.HasTile(cellPosition))
        {
            return Color.blue; // AZUL = CENÁRIO
        }

        // Tile vazio - potencial fuga
        return Color.white; // BRANCO = VAZIO (seguro)
    }

    // Função para forçar mostrar debug (pode chamar no inspector)
    [ContextMenu("Toggle Debug Visual")]
    public void ToggleDebugVisual()
    {
        isFleeingFromBomb = !isFleeingFromBomb; // Força mostrar gizmos
    }

    private void DrawSurroundingsGizmos(Vector3Int currentCell)
    {
        // Só desenha quando está fugindo ou colocando bomba (para não poluir sempre)
        if (!isFleeingFromBomb && !isPlacingBomb) return;

        // Encontra o tile seguro mais próximo
        Vector3Int nearestSafeTile = FindNearestSafeTile(currentCell);

        // === NOVA PARTE: DESENHA O CAMINHO ATÉ O TILE SEGURO ===
        if (nearestSafeTile != currentCell)
        {
            DrawPathToSafeTile(currentCell, nearestSafeTile);
        }

        // Verifica raio 1 e BotRadiusSearch
        for (int radius = 1; radius <= BotRadiusSearch; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    // Pula o centro
                    if (x == 0 && y == 0) continue;

                    // Para raio 2, só verifica as bordas (evita repetir raio 1)
                    if (radius == 2 && Mathf.Abs(x) < 2 && Mathf.Abs(y) < 2) continue;

                    Vector3Int checkCell = currentCell + new Vector3Int(x, y, 0);
                    Vector3 worldPos = undestructibleTiles.GetCellCenterWorld(checkCell);

                    // Define cor baseado no tipo de tile
                    Color gizmoColor = GetGizmoColorForTile(checkCell);

                    // === DESTAQUE PARA O TILE SEGURO MAIS PRÓXIMO ===
                    if (checkCell == nearestSafeTile)
                    {
                        gizmoColor = Color.cyan; // CIANO = MELHOR OPÇÃO DE FUGA
                    }

                    Gizmos.color = gizmoColor;

                    // Desenha cubo sólido para tiles importantes
                    if (gizmoColor != Color.white)
                    {
                        Gizmos.DrawCube(worldPos, Vector3.one * 0.3f);
                    }
                    else
                    {
                        // Desenha contorno para tiles vazios
                        Gizmos.DrawWireCube(worldPos, Vector3.one * 0.3f);
                    }

                    // === DESTAQUE EXTRA PARA O TILE SEGURO MAIS PRÓXIMO ===
                    if (checkCell == nearestSafeTile)
                    {
                        // Desenha um anel pulsante ao redor
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireSphere(worldPos, 0.5f);
                        Gizmos.DrawWireSphere(worldPos, 0.4f);

                        // Desenha seta apontando para ele
                        Vector3 botWorldPos = undestructibleTiles.GetCellCenterWorld(currentCell);
                        Vector3 direction = (worldPos - botWorldPos).normalized;
                        Vector3 arrowStart = botWorldPos + direction * 0.3f;
                        Vector3 arrowEnd = worldPos - direction * 0.3f;

                        Gizmos.color = Color.green;
                        Gizmos.DrawLine(arrowStart, arrowEnd);

                        // Desenha cabeça da seta
                        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0);
                        Gizmos.DrawLine(arrowEnd, arrowEnd - direction * 0.1f + perpendicular * 0.1f);
                        Gizmos.DrawLine(arrowEnd, arrowEnd - direction * 0.1f - perpendicular * 0.1f);
                    }

                    // === NOVA PARTE: DEBUG VISUAL DOS ARREDORES ===
                    DrawBombsInCell(checkCell, worldPos);

                    // Desenha texto do raio (opcional - pode ser muito poluído)
#if UNITY_EDITOR
                string label = $"R{radius}";
                if (checkCell == nearestSafeTile)
                {
                    int manhattanDistance = Mathf.Abs(checkCell.x - currentCell.x) + Mathf.Abs(checkCell.y - currentCell.y);
                    label += $" SAFE! M{manhattanDistance}";
                    UnityEditor.Handles.color = Color.green;
                }
                else
                {
                    UnityEditor.Handles.color = Color.white;
                }
                UnityEditor.Handles.Label(worldPos + Vector3.up * 0.3f, label);
#endif
                }
            }
        }
    }

    // === NOVA FUNÇÃO: DESENHA O CAMINHO COMPLETO ATÉ O TILE SEGURO ===
    private void DrawPathToSafeTile(Vector3Int start, Vector3Int target)
    {
        // Encontra o melhor caminho Manhattan (horizontal primeiro ou vertical primeiro)
        List<Vector3Int> bestPath = FindBestManhattanPath(start, target);

        if (bestPath == null || bestPath.Count <= 1)
        {
            // Não há caminho válido - desenha linha vermelha pontilhada para mostrar que não é alcançável
            Vector3 startWorldPos2 = undestructibleTiles.GetCellCenterWorld(start);
            Vector3 targetWorldPos = undestructibleTiles.GetCellCenterWorld(target);

            Gizmos.color = Color.red;
            DrawDottedLine(startWorldPos2, targetWorldPos, 0.2f);

#if UNITY_EDITOR
        Vector3 midPoint = (startWorldPos2 + targetWorldPos) * 0.5f;
        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.Label(midPoint, "NO PATH!");
#endif

            return;
        }

        // Desenha o caminho passo a passo
        for (int i = 0; i < bestPath.Count - 1; i++)
        {
            Vector3Int currentStep = bestPath[i];
            Vector3Int nextStep = bestPath[i + 1];

            Vector3 currentWorldPos = undestructibleTiles.GetCellCenterWorld(currentStep);
            Vector3 nextWorldPos = undestructibleTiles.GetCellCenterWorld(nextStep);

            // Cor do caminho - verde brilhante para mostrar que é o caminho de fuga
            Gizmos.color = Color.green;

            // Desenha linha grossa entre os pontos
            DrawThickLine(currentWorldPos, nextWorldPos, 0.1f);

            // Desenha seta indicando direção
            Vector3 direction = (nextWorldPos - currentWorldPos).normalized;
            Vector3 arrowPos = currentWorldPos + direction * 0.7f;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0);

            Gizmos.DrawLine(arrowPos, arrowPos + direction * 0.2f + perpendicular * 0.1f);
            Gizmos.DrawLine(arrowPos, arrowPos + direction * 0.2f - perpendicular * 0.1f);

            // Desenha círculo pequeno em cada passo do caminho
            Gizmos.color = Color.green * 0.8f;
            Gizmos.DrawSphere(nextWorldPos, 0.08f);

#if UNITY_EDITOR
        // Mostra número do passo
        UnityEditor.Handles.color = Color.green;
        UnityEditor.Handles.Label(nextWorldPos + Vector3.up * 0.2f, $"{i + 1}");
#endif
        }

#if UNITY_EDITOR
    // Mostra informações do caminho
    Vector3 startWorldPos = undestructibleTiles.GetCellCenterWorld(start);
    UnityEditor.Handles.color = Color.green;
    UnityEditor.Handles.Label(startWorldPos + Vector3.down * 0.5f, $"PATH: {bestPath.Count - 1} steps");
#endif
    }

    // === FUNÇÕES AUXILIARES PARA DESENHO ===
    private void DrawThickLine(Vector3 start, Vector3 end, float thickness)
    {
        // Desenha várias linhas paralelas para simular espessura
        Vector3 direction = (end - start).normalized;
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0) * thickness;

        Gizmos.DrawLine(start + perpendicular, end + perpendicular);
        Gizmos.DrawLine(start, end);
        Gizmos.DrawLine(start - perpendicular, end - perpendicular);
    }

    private void DrawDottedLine(Vector3 start, Vector3 end, float dashLength)
    {
        Vector3 direction = end - start;
        float totalDistance = direction.magnitude;
        direction.Normalize();

        float currentDistance = 0;
        bool drawDash = true;

        while (currentDistance < totalDistance)
        {
            float nextDistance = Mathf.Min(currentDistance + dashLength, totalDistance);

            if (drawDash)
            {
                Vector3 dashStart = start + direction * currentDistance;
                Vector3 dashEnd = start + direction * nextDistance;
                Gizmos.DrawLine(dashStart, dashEnd);
            }

            currentDistance = nextDistance;
            drawDash = !drawDash;
        }
    }

    private bool IsInExplosionPath(Vector3Int targetPos, Vector3Int bombPos, int explosionRadius)
    {
        // Verifica as 4 direções: up, down, left, right
        Vector3Int[] directions = {
        Vector3Int.up,
        Vector3Int.down,
        Vector3Int.left,
        Vector3Int.right
    };

        foreach (Vector3Int direction in directions)
        {
            // Verifica cada posição na direção até o raio máximo
            for (int distance = 1; distance <= explosionRadius; distance++)
            {
                Vector3Int checkPos = bombPos + (direction * distance);

                // Se encontrou a posição target, está em perigo
                if (checkPos == targetPos)
                {
                    // Mas precisa verificar se a explosão não é bloqueada antes
                    if (!IsExplosionBlocked(bombPos, direction, distance))
                    {
                        return true; // PERIGO!
                    }
                }
            }
        }

        return false; // Não está no caminho de explosão
    }

    private bool IsExplosionBlocked(Vector3Int bombPos, Vector3Int direction, int maxDistance)
    {
        for (int distance = 1; distance < maxDistance; distance++)
        {
            Vector3Int checkPos = bombPos + (direction * distance);

            // Se encontra parede indestrutível, bloqueia
            if (undestructibleTiles != null && undestructibleTiles.HasTile(checkPos))
            {
                return true; // Bloqueado
            }

            // Se encontra parede destrutível, bloqueia (mas a explosão ainda atinge essa posição)
            if (destructibleTiles != null && destructibleTiles.HasTile(checkPos))
            {
                return true; // Bloqueado após essa posição
            }
        }

        return false; // Caminho livre
    }

    private IEnumerator PlaceBombAndFlee(Vector3Int destructibleCell)
    {
        isPlacingBomb = true;
        StopMovement();

        // Coloca bomba no tile atual
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3 bombPosition = undestructibleTiles.GetCellCenterWorld(currentCell);

        GameObject bombObj = Instantiate(bombPrefab, bombPosition, Quaternion.identity);
        bombsRemaining--;
        lastBombPosition = currentCell;
        bombPlacedTime = Time.time;

        activeBombPositions.Add(currentCell);

        Bomb bomb = bombObj.GetComponent<Bomb>();
        bomb.Init(bombFuseTime, explosionRadius, explosionDuration, explosionPrefab,
                  explosionLayerMask, destructibleTiles, undestructibleTiles,
                  destructiblePrefab, itemDestructiblePrefab);

        yield return null; // aguarda um frame para inicializar

        isPlacingBomb = false;
        isFleeingFromBomb = true;

        // === MUDANÇA PRINCIPAL: USA O MESMO SISTEMA DOS GIZMOS ===
        Vector3Int safeTile = FindNearestSafeTile(currentCell);

        if (safeTile != currentCell)
        {
            ExecuteFleeToPositionManhattan(safeTile);
        }
        else
        {
            EmergencyFlee();
        }

        // Monitorar explosão da bomba
        StartCoroutine(MonitorBombExplosion(bombObj, currentCell));
    }

    private void ExecuteFleeToPositionManhattan(Vector3Int safePosition)
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);

        List<Vector3Int> manhattanPath = FindBestManhattanPath(currentCell, safePosition);

        if (manhattanPath != null && manhattanPath.Count > 1)
        {
            pathQueue.Clear();


            // Converte células para posições mundo CENTRALIZADAS
            for (int i = 1; i < manhattanPath.Count; i++)
            {
                Vector3Int cellPos = manhattanPath[i];

                // === CORREÇÃO: SEMPRE USA CENTRO DA CÉLULA ===
                Vector3 worldPosCenter = undestructibleTiles.GetCellCenterWorld(cellPos);


                pathQueue.Enqueue(worldPosCenter);
            }


            if (!isMovingToTarget)
                MoveToNextTarget();
        }
        else
        {
            EmergencyFlee();
        }
    }

    // === MELHORADA: FUGA DE EMERGÊNCIA MAIS INTELIGENTE ===
    private void EmergencyFlee()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        Vector3Int[] emergencyDirections = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        // Ordena direções por segurança (mais longe de bombas é melhor)
        System.Array.Sort(emergencyDirections, (dir1, dir2) =>
        {
            Vector3Int cell1 = currentCell + dir1;
            Vector3Int cell2 = currentCell + dir2;

            bool safe1 = IsTileSafe(cell1);
            bool safe2 = IsTileSafe(cell2);

            if (safe1 && !safe2) return -1; // cell1 é melhor
            if (!safe1 && safe2) return 1;  // cell2 é melhor

            return 0; // iguais
        });

        foreach (Vector3Int dir in emergencyDirections)
        {
            Vector3Int testCell = currentCell + dir;

            // Verifica se é transitável e minimamente seguro
            if (!IsObstacle(testCell))
            {
                Vector3 emergencyTarget = undestructibleTiles.GetCellCenterWorld(testCell);
                pathQueue.Clear();
                pathQueue.Enqueue(emergencyTarget);

                if (!isMovingToTarget)
                {
                    MoveToNextTarget();
                }
                return; // Sai assim que encontra uma opção
            }
        }
    }

    // === FUNÇÃO MELHORADA: VERIFICA SE TILE É REALMENTE SEGURO ===
    private bool IsTileSafe(Vector3Int cellPosition)
    {
        // Deve ser cenário (azul) E estar seguro de bombas
        bool isCenaryTile = (scenary != null && scenary.HasTile(cellPosition));
        bool isSafeFromBombs = IsSafeFromBombsWithRadius(cellPosition);

        // Também não pode ter obstáculos que impedem o movimento
        bool isWalkable = !IsObstacle(cellPosition);

        return isCenaryTile && isSafeFromBombs && isWalkable;
    }

    // === CORRIGE A FUNÇÃO PARA SER MAIS PRECISA ===
    private bool IsSafeFromBombsWithRadius(Vector3Int cellPosition)
    {
        // Encontra todas as bombas ativas no jogo
        Bomb[] allBombs = FindObjectsOfType<Bomb>();

        foreach (Bomb bomb in allBombs)
        {
            Vector3Int bombCellPos = undestructibleTiles.WorldToCell(bomb.transform.position);

            // Se a célula é a mesma da bomba, não é segura
            if (cellPosition == bombCellPos)
            {
                return false;
            }

            // Verifica se está na linha de explosão da bomba
            if (IsInExplosionPath(cellPosition, bombCellPos, bomb.explosionRadius))
            {
                return false;
            }
        }

        // Também verifica bombas na lista interna (backup)
        foreach (Vector3Int bombPos in activeBombPositions)
        {
            if (cellPosition == bombPos)
            {
                return false;
            }

            // Usa o raio padrão se não conseguir acessar a bomba
            if (IsInExplosionPath(cellPosition, bombPos, explosionRadius))
            {
                return false;
            }
        }

        return true; // Seguro se não está em nenhuma área de explosão
    }

    // === JÁ EXISTENTES - MANTÉM COMO ESTÃO ===
    private Vector3Int FindNearestSafeTile(Vector3Int botPosition)
    {
        Vector3Int nearestSafe = botPosition;
        int nearestDistance = int.MaxValue;
        bool foundSafe = false;

        // Procura em um raio maior (até 5 tiles de distância Manhattan)
        for (int manhattanRadius = 1; manhattanRadius <= 5; manhattanRadius++)
        {
            // Verifica todos os pontos neste raio Manhattan
            for (int x = -manhattanRadius; x <= manhattanRadius; x++)
            {
                for (int y = -manhattanRadius; y <= manhattanRadius; y++)
                {
                    // Só considera pontos que estão exatamente na distância Manhattan atual
                    int currentManhattanDistance = Mathf.Abs(x) + Mathf.Abs(y);
                    if (currentManhattanDistance != manhattanRadius) continue;

                    Vector3Int checkCell = botPosition + new Vector3Int(x, y, 0);

                    // Verifica se é um tile seguro (cenário/azul)
                    if (IsTileSafe(checkCell))
                    {
                        // Verifica se o caminho Manhattan está livre
                        if (IsManhattanPathClear(botPosition, checkCell))
                        {
                            if (currentManhattanDistance < nearestDistance)
                            {
                                nearestDistance = currentManhattanDistance;
                                nearestSafe = checkCell;
                                foundSafe = true;
                            }
                        }
                    }
                }
            }

            // Se encontrou algo seguro neste raio, para de procurar mais longe
            if (foundSafe) break;
        }

        return nearestSafe;
    }

    private List<Vector3Int> FindBestManhattanPath(Vector3Int start, Vector3Int target)
    {
        // Testa ambos os caminhos Manhattan possíveis
        List<Vector3Int> path1 = GetManhattanPath(start, target, true);  // horizontal primeiro
        List<Vector3Int> path2 = GetManhattanPath(start, target, false); // vertical primeiro

        // Se apenas um é válido, retorna ele
        if (path1 != null && path2 == null) return path1;
        if (path2 != null && path1 == null) return path2;
        if (path1 == null && path2 == null) return null;

        // Se ambos são válidos, escolhe o que tem menos mudanças de direção (mais direto)
        // Para Manhattan, ambos têm o mesmo número de passos, então qualquer um serve
        // Vamos preferir horizontal primeiro como padrão
        return path1;
    }

    private List<Vector3Int> GetManhattanPath(Vector3Int start, Vector3Int target, bool horizontalFirst)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        Vector3Int current = start;
        path.Add(current);

        Vector3Int diff = target - start;

        try
        {
            if (horizontalFirst)
            {
                // Primeiro movimento horizontal
                int horizontalSteps = diff.x;
                for (int i = 0; i < Mathf.Abs(horizontalSteps); i++)
                {
                    current.x += horizontalSteps > 0 ? 1 : -1;
                    if (IsObstacle(current)) return null; // Caminho bloqueado
                    path.Add(current);
                }

                // Depois movimento vertical
                int verticalSteps = diff.y;
                for (int i = 0; i < Mathf.Abs(verticalSteps); i++)
                {
                    current.y += verticalSteps > 0 ? 1 : -1;
                    if (IsObstacle(current)) return null; // Caminho bloqueado
                    path.Add(current);
                }
            }
            else
            {
                // Primeiro movimento vertical
                int verticalSteps = diff.y;
                for (int i = 0; i < Mathf.Abs(verticalSteps); i++)
                {
                    current.y += verticalSteps > 0 ? 1 : -1;
                    if (IsObstacle(current)) return null; // Caminho bloqueado
                    path.Add(current);
                }

                // Depois movimento horizontal
                int horizontalSteps = diff.x;
                for (int i = 0; i < Mathf.Abs(horizontalSteps); i++)
                {
                    current.x += horizontalSteps > 0 ? 1 : -1;
                    if (IsObstacle(current)) return null; // Caminho bloqueado
                    path.Add(current);
                }
            }
        }
        catch (System.Exception)
        {
            return null; // Erro no cálculo
        }

        return path;
    }

    private bool IsManhattanPathClear(Vector3Int start, Vector3Int target)
    {
        // Calcula a diferença
        Vector3Int diff = target - start;

        // Existem duas possibilidades de caminho Manhattan: horizontal->vertical ou vertical->horizontal
        // Vamos testar ambas e retornar true se pelo menos uma estiver livre

        bool path1Clear = IsManhattanPathClearOneWay(start, target, true);  // horizontal primeiro
        bool path2Clear = IsManhattanPathClearOneWay(start, target, false); // vertical primeiro

        return path1Clear || path2Clear;
    }

    private bool IsManhattanPathClearOneWay(Vector3Int start, Vector3Int target, bool horizontalFirst)
    {
        Vector3Int diff = target - start;
        Vector3Int current = start;

        if (horizontalFirst)
        {
            // Primeiro movimento horizontal
            int horizontalSteps = diff.x;
            for (int i = 0; i < Mathf.Abs(horizontalSteps); i++)
            {
                current.x += horizontalSteps > 0 ? 1 : -1;
                if (IsObstacle(current)) return false;
            }

            // Depois movimento vertical
            int verticalSteps = diff.y;
            for (int i = 0; i < Mathf.Abs(verticalSteps); i++)
            {
                current.y += verticalSteps > 0 ? 1 : -1;
                if (IsObstacle(current)) return false;
            }
        }
        else
        {
            // Primeiro movimento vertical
            int verticalSteps = diff.y;
            for (int i = 0; i < Mathf.Abs(verticalSteps); i++)
            {
                current.y += verticalSteps > 0 ? 1 : -1;
                if (IsObstacle(current)) return false;
            }

            // Depois movimento horizontal
            int horizontalSteps = diff.x;
            for (int i = 0; i < Mathf.Abs(horizontalSteps); i++)
            {
                current.x += horizontalSteps > 0 ? 1 : -1;
                if (IsObstacle(current)) return false;
            }
        }

        return true; // Caminho livre!
    }

    private bool IsObstacle(Vector3Int cellPosition)
    {
        // Parede indestrutível (preto)
        if (undestructibleTiles != null && undestructibleTiles.HasTile(cellPosition))
            return true;

        // Parede destrutível (amarelo)
        if (destructibleTiles != null && destructibleTiles.HasTile(cellPosition))
            return true;

        return false;
    }

    // === SISTEMA DE MONITORAMENTO CONTÍNUO ===
    private IEnumerator MonitorBombSafety()
    {
        while (isFleeingFromBomb)
        {
            Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
            bool stillInDanger = false;

            GameObject[] allBombs = GameObject.FindGameObjectsWithTag("Bomb");

            foreach (GameObject bombObj in allBombs)
            {
                Bomb bomb = bombObj.GetComponent<Bomb>();
                if (bomb != null && !bomb.IsExploded && bomb.IsDangerous(currentCell, undestructibleTiles))
                {
                    if (bomb.RemainingTime > 0.2f) // Ainda há perigo real
                    {
                        stillInDanger = true;
                        break;
                    }
                }
            }

            if (!stillInDanger)
            {
                StopFleeingFromBomb();
                yield break;
            }

            yield return new WaitForSeconds(0.2f); // Verifica a cada 0.2s
        }
    }

    private void StartPreciseFleeFromBomb(Bomb dangerousBomb)
    {
        Vector3Int bombCell = undestructibleTiles.WorldToCell(dangerousBomb.transform.position);
        float remainingTime = dangerousBomb.RemainingTime;

        isFleeingFromBomb = true;

        // === CÁLCULO PRECISO DO TEMPO DE FUGA ===
        float fleeTime;

        if (remainingTime > 2f)
        {
            // Tempo suficiente - usa o tempo restante + margem pequena
            fleeTime = remainingTime + 0.3f;
        }
        else if (remainingTime > 0.5f)
        {
            // Tempo apertado - usa tempo restante + margem mínima
            fleeTime = remainingTime + 0.1f;
        }
        else
        {
            // Situação crítica - fuga de emergência imediata
            fleeTime = 0.8f; // Tempo mínimo para sair da zona
        }

        // Inicia timer de fuga
        fleeTimerCoroutine = StartCoroutine(FleeTimer(fleeTime));

        // Encontra posição segura
        Vector3Int safePosition = FindSafePositionAwayFromBomb(bombCell, dangerousBomb.explosionRadius);

        if (safePosition != Vector3Int.zero)
        {
            ExecuteFleeToPositionManhattan(safePosition);
        }
        else
        {
            EmergencyFlee();
        }
    }
    // === FUNÇÃO PARA ENCONTRAR POSIÇÃO SEGURA LONGE DE UMA BOMBA ===

    private Vector3Int FindSafePositionAwayFromBomb(Vector3Int bombPos, int bombRadius)
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        int safeDistance = bombRadius + 2; // +2 margem extra

        // Tenta posições em ordem de prioridade
        Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };

        foreach (Vector3Int dir in directions)
        {
            for (int dist = safeDistance; dist <= safeDistance + 3; dist++)
            {
                Vector3Int testPos = currentCell + (dir * dist);

                // Verifica se está longe o suficiente da bomba
                int distanceToBomb = Mathf.Abs(testPos.x - bombPos.x) + Mathf.Abs(testPos.y - bombPos.y);

                if (distanceToBomb >= safeDistance && IsTileSafe(testPos))
                {
                    return testPos;
                }
            }
        }


        foreach (Vector3Int dir in directions)
        {
            for (int dist = bombRadius + 1; dist <= bombRadius + 4; dist++)
            {
                Vector3Int testPos = currentCell + (dir * dist);

                // Verifica se pelo menos não é obstacle
                if (!IsObstacle(testPos))
                {
                    int distanceToBomb = Mathf.Abs(testPos.x - bombPos.x) + Mathf.Abs(testPos.y - bombPos.y);
                    return testPos;
                }
            }
        }
        return Vector3Int.zero;
    }

    private void CheckPreciseBombDanger()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        GameObject[] allBombs = GameObject.FindGameObjectsWithTag("Bomb");

        Bomb mostDangerousBomb = null;
        float shortestTime = float.MaxValue;

        foreach (GameObject bombObj in allBombs)
        {
            Bomb bomb = bombObj.GetComponent<Bomb>();
            if (bomb != null && !bomb.IsExploded)
            {
                // Verifica se está na zona de perigo
                if (bomb.IsDangerous(currentCell, undestructibleTiles))
                {
                    float remainingTime = bomb.RemainingTime;

                    // Encontra a bomba com menor tempo restante
                    if (remainingTime < shortestTime)
                    {
                        shortestTime = remainingTime;
                        mostDangerousBomb = bomb;
                    }
                }
            }
        }

        // Se encontrou bomba perigosa, foge
        if (mostDangerousBomb != null)
        {
            StartPreciseFleeFromBomb(mostDangerousBomb);
        }
    }
    
    // === VERSÃO APRIMORADA PARA MÚLTIPLAS BOMBAS ===
    private void CheckMultipleBombDanger()
    {
        Vector3Int currentCell = undestructibleTiles.WorldToCell(transform.position);
        GameObject[] allBombs = GameObject.FindGameObjectsWithTag("Bomb");

        System.Collections.Generic.List<Bomb> dangerousBombs = new System.Collections.Generic.List<Bomb>();

        // Identifica todas as bombas perigosas
        foreach (GameObject bombObj in allBombs)
        {
            Bomb bomb = bombObj.GetComponent<Bomb>();
            if (bomb != null && !bomb.IsExploded && bomb.IsDangerous(currentCell, undestructibleTiles))
            {
                dangerousBombs.Add(bomb);
            }
        }

        if (dangerousBombs.Count > 0)
        {

            // Ordena por tempo restante (menor primeiro)
            dangerousBombs.Sort((a, b) => a.RemainingTime.CompareTo(b.RemainingTime));

            // Foge da mais perigosa
            Bomb mostUrgent = dangerousBombs[0];


            StartPreciseFleeFromBomb(mostUrgent);
        }
    }
}