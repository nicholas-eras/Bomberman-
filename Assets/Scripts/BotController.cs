// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.Tilemaps;

// [RequireComponent(typeof(MovementController))]
// public class BotController : MonoBehaviour
// {
//     private MovementController movement;
//     private BombController bombController;
//     private GameObject targetPlayer;
//     private Vector2 currentDirection;
//     private float decisionTimer = 0f;
//     private float decisionCooldown = 0.5f;

//     private LayerMask obstacleMask;
//     private LayerMask dangerMask;
//     private LayerMask itemMask;
//     private LayerMask playerMask;
//     private LayerMask destructibleMask;

//     [Header("Bot Settings")]
//     [Range(1f, 15f)]
//     public float destructibleSearchRadius = 8f;
//     [Range(1f, 15f)]
//     public float itemSearchRadius = 8f;
//     [Range(1f, 15f)]
//     public float playerSearchRadius = 8f;
    
//     private Vector2 lastBombPosition = Vector2.zero;
//     private float bombPlacedTime = 0f;
//     private bool isEscaping = false; // Mudança: controle mais específico de fuga
//     private Vector2 escapeDirection = Vector2.zero; // Direção de fuga definida
//     private float escapeStartTime = 0f;

//     private void Awake()
//     {
//         movement = GetComponent<MovementController>();
//         bombController = GetComponent<BombController>();

//         obstacleMask = LayerMask.GetMask("Stage");
//         dangerMask = LayerMask.GetMask("Explosion");
//         itemMask = LayerMask.GetMask("Items");
//         playerMask = LayerMask.GetMask("Player");
//         destructibleMask = LayerMask.GetMask("Destructible");
//     }

//     private float stuckTimer = 0f;
//     private Vector2 lastPosition = Vector2.zero;

//     private void OnDrawGizmos()
//     {
//         if (Application.isPlaying)
//         {
//             Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
//             foreach (var dir in dirs)
//             {
//                 if (CanMove(dir))
//                 {
//                     Vector2 newPos = (Vector2)transform.position + dir * 2f;
//                     Gizmos.color = IsSafePosition(newPos) ? Color.green : Color.red;
//                     Gizmos.DrawLine(transform.position, newPos);
//                     Gizmos.DrawSphere(newPos, 0.2f);
//                 }
//             }
            
//             if (lastBombPosition != Vector2.zero)
//             {
//                 Gizmos.color = Color.yellow;
//                 Gizmos.DrawWireSphere(lastBombPosition, 2.5f);
//                 Gizmos.DrawLine(transform.position, lastBombPosition);
//             }

//             // Desenha direção de fuga
//             if (isEscaping && escapeDirection != Vector2.zero)
//             {
//                 Gizmos.color = Color.cyan;
//                 Gizmos.DrawRay(transform.position, escapeDirection * 3f);
//             }
//         }
//     }

//     private void Update()
//     {
//         decisionTimer -= Time.deltaTime;

//         // Verifica se está preso
//         if (Vector2.Distance(transform.position, lastPosition) < 0.1f)
//         {
//             stuckTimer += Time.deltaTime;
//             if (stuckTimer > 1.5f) // Reduzido o tempo para reagir mais rápido
//             {
//                 Debug.Log("Bot está preso! Forçando nova direção...");
//                 ForceNewDirection();
//                 stuckTimer = 0f;
//             }
//         }
//         else
//         {
//             stuckTimer = 0f;
//         }

//         lastPosition = transform.position;

//         if (decisionTimer <= 0f)
//         {
//             decisionTimer = decisionCooldown;
//             Think();
//         }

//         Move();
//     }

//     private void ForceNewDirection()
//     {
//         // Para de escapar se estava escapando
//         isEscaping = false;
//         escapeDirection = Vector2.zero;
        
//         // Encontra uma nova direção válida
//         currentDirection = GetRandomValidDirection();
        
//         // Se ainda está preso, tenta forçar movimento mesmo com obstáculos menores
//         if (currentDirection == Vector2.zero)
//         {
//             Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
//             foreach (var dir in dirs)
//             {
//                 // Verifica com raio menor para tentar passar por espaços apertados
//                 RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, 0.3f, obstacleMask);
//                 if (hit.collider == null)
//                 {
//                     currentDirection = dir;
//                     break;
//                 }
//             }
//         }
//     }

//     private void Think()
//     {
//         // 1. PRIORIDADE MÁXIMA: Se está em perigo de explosão imediata
//         if (IsInDanger())
//         {
//             Debug.Log("Bot em perigo imediato! Fugindo!");
//             currentDirection = FindSafeDirection();
//             isEscaping = false; // Sai do modo de fuga planejada
//             return;
//         }

//         // 2. FUGA PÓS-BOMBA: Se acabou de colocar bomba, continua fugindo
//         if (isEscaping && Time.time - escapeStartTime < 4f)
//         {
//             float distanceFromBomb = Vector2.Distance(transform.position, lastBombPosition);
            
//             // Aumentou distância mínima e tempo de fuga obrigatória
//             if (distanceFromBomb >= 4f && IsSafePosition(transform.position) && Time.time - escapeStartTime > 2f)
//             {
//                 Debug.Log($"Bot parou de fugir - distância da bomba: {distanceFromBomb:F2}");
//                 isEscaping = false;
//                 currentDirection = Vector2.zero; // Para por um momento
//                 return;
//             }
            
//             // Continua na direção de fuga se ainda é válida
//             if (CanMove(escapeDirection) && IsSafeToMoveTo(escapeDirection))
//             {
//                 currentDirection = escapeDirection;
//                 Debug.Log($"Continuando fuga na direção: {escapeDirection}");
//                 return;
//             }
//             else
//             {
//                 // Se a direção de fuga não é mais válida, encontra nova
//                 Vector2 newEscapeDir = FindSafeDirection();
//                 if (newEscapeDir != Vector2.zero)
//                 {
//                     escapeDirection = newEscapeDir;
//                     currentDirection = newEscapeDir;
//                     Debug.Log($"Nova direção de fuga: {newEscapeDir}");
//                     return;
//                 }
//             }
//         }
//         else
//         {
//             isEscaping = false; // Terminou o período de fuga
//         }

//         // 3. Buscar itens
//         GameObject item = FindNearest(itemMask, itemSearchRadius);
//         if (item != null)
//         {
//             currentDirection = GetDirectionTo(item.transform.position);
//             return;
//         }

//         // 4. Perseguir e atacar jogadores
//         targetPlayer = FindNearest(playerMask, playerSearchRadius);
//         if (targetPlayer != null)
//         {
//             float distanceToPlayer = Vector2.Distance(transform.position, targetPlayer.transform.position);

//             if (distanceToPlayer <= 2f && bombController.CanPlaceBomb())
//             {
//                 Vector2 safeEscapeDir = FindBestEscapeDirection(transform.position);
//                 if (safeEscapeDir != Vector2.zero)
//                 {
//                     Debug.Log("Colocando bomba no player e iniciando fuga planejada");
//                     PlaceBombAndEscape(safeEscapeDir);
//                     return;
//                 }
//             }

//             Vector2 dirToPlayer = GetDirectionTo(targetPlayer.transform.position);
//             if (IsSafeToMoveTo(dirToPlayer))
//             {
//                 currentDirection = dirToPlayer;
//             }
//             else
//             {
//                 currentDirection = FindSafeDirection();
//             }
//             return;
//         }

//         // 5. Destruir tiles destrutíveis
//         GameObject destructible = FindNearestDestructible();
//         if (destructible != null)
//         {
//             Vector2 destructiblePos = destructible.transform.position;
//             float distanceToDestructible = Vector2.Distance(transform.position, destructiblePos);

//             Debug.Log($"Bot viu tile destrutível em {destructiblePos}! Distância: {distanceToDestructible:F2}");

//             if (distanceToDestructible <= 1.5f && bombController.CanPlaceBomb())
//             {
//                 Vector2 safeEscapeDir = FindBestEscapeDirection(transform.position);
//                 if (safeEscapeDir != Vector2.zero)
//                 {
//                     Debug.Log("Colocando bomba no destrutível e iniciando fuga planejada");
//                     PlaceBombAndEscape(safeEscapeDir);
//                     return;
//                 }
//                 else
//                 {
//                     Debug.Log("Bot NÃO colocou bomba - nenhuma rota de fuga válida!");
//                     // Se não tem fuga, afasta-se primeiro
//                     currentDirection = GetRandomValidDirection();
//                     return;
//                 }
//             }
//             else if (distanceToDestructible > 1.5f)
//             {
//                 currentDirection = GetDirectionTo(destructiblePos);
//                 Debug.Log($"Movendo em direção ao tile destrutível: {currentDirection}");
//                 return;
//             }
//         }

//         // 6. Exploração
//         currentDirection = GetExplorationDirection();
//     }

//     // NOVA FUNÇÃO: Coloca bomba e inicia fuga planejada
//     private void PlaceBombAndEscape(Vector2 escapeDir)
//     {
//         bombController.PlaceBomb();
//         lastBombPosition = transform.position; // Usa posição atual, não do BombController
//         bombPlacedTime = Time.time;
        
//         // Inicia fuga planejada
//         isEscaping = true;
//         escapeDirection = escapeDir;
//         escapeStartTime = Time.time;
//         currentDirection = escapeDir;
        
//         Debug.Log($"Bomba colocada em {lastBombPosition}, fugindo para {escapeDir}");
        
//         // Movimento imediato na direção de fuga
//         Move();
//     }

//     // NOVA FUNÇÃO: Encontra a melhor direção para escapar de uma posição
//     private Vector2 FindBestEscapeDirection(Vector2 bombPosition)
//     {
//         Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
//         List<Vector2> validEscapeDirections = new List<Vector2>();
        
//         Debug.Log($"Testando direções de fuga da posição {bombPosition}");
        
//         foreach (var dir in directions)
//         {
//             Debug.Log($"Testando direção {dir}");
            
//             // PRIMEIRO: verifica se pode mover nessa direção imediatamente
//             if (!CanMove(dir))
//             {
//                 Debug.Log($"Não pode mover na direção {dir} - obstáculo imediato");
//                 continue;
//             }
            
//             // SEGUNDO: simula movimento consecutivo nessa direção
//             Vector2 currentTestPos = bombPosition;
//             bool canEscapeFully = true;
//             int escapeDistance = 0;
            
//             // Testa movimento step by step
//             for (int step = 1; step <= 4; step++)
//             {
//                 Vector2 nextPos = bombPosition + dir * step;
                
//                 // Verifica se pode mover para essa próxima posição
//                 RaycastHit2D obstacleCheck = Physics2D.Raycast(currentTestPos, dir, 1f, obstacleMask);
//                 if (obstacleCheck.collider != null)
//                 {
//                     Debug.Log($"Obstáculo encontrado no step {step} da direção {dir}");
//                     break;
//                 }
                
//                 // Verifica se essa posição será segura da bomba
//                 if (WillBeSafeFromBombAt(nextPos, bombPosition))
//                 {
//                     escapeDistance = step;
//                     Debug.Log($"Posição segura encontrada no step {step} da direção {dir}");
//                     if (step >= 2) // Pelo menos 2 tiles de distância
//                     {
//                         canEscapeFully = true;
//                         break;
//                     }
//                 }
                
//                 currentTestPos = nextPos;
//             }
            
//             if (canEscapeFully && escapeDistance >= 2)
//             {
//                 validEscapeDirections.Add(dir);
//                 Debug.Log($"Direção {dir} aprovada para fuga (distância: {escapeDistance})");
//             }
//             else
//             {
//                 Debug.Log($"Direção {dir} rejeitada - fuga insuficiente (distância: {escapeDistance})");
//             }
//         }
        
//         if (validEscapeDirections.Count > 0)
//         {
//             // Escolhe a direção que mais se afasta do centro do mapa
//             Vector2 bestDirection = validEscapeDirections[0];
//             Debug.Log($"Escolhida direção de fuga: {bestDirection}");
//             return bestDirection;
//         }
        
//         Debug.LogError("NENHUMA direção de fuga válida encontrada! Bot não deve colocar bomba aqui!");
//         return Vector2.zero;
//     }

//     private void Move()
//     {
//         if (CanMove(currentDirection))
//         {
//             if (currentDirection == Vector2.up)
//                 movement.SetDirection(Vector2.up, movement.spriteRendererUp);
//             else if (currentDirection == Vector2.down)
//                 movement.SetDirection(Vector2.down, movement.spriteRendererDown);
//             else if (currentDirection == Vector2.left)
//                 movement.SetDirection(Vector2.left, movement.spriteRendererLeft);
//             else if (currentDirection == Vector2.right)
//                 movement.SetDirection(Vector2.right, movement.spriteRendererRight);
//             else
//                 movement.SetDirection(Vector2.zero, movement.activeSpriteRenderer);
//         }
//         else
//         {
//             Vector2 newDir = GetRandomValidDirection();
//             if (newDir != Vector2.zero)
//             {
//                 currentDirection = newDir;
//             }
//             else
//             {
//                 movement.SetDirection(Vector2.zero, movement.activeSpriteRenderer);
//             }
//         }
//     }

//     private bool WillBeSafeFromBombAt(Vector2 safePos, Vector2 bombPos)
//     {
//         float distance = Vector2.Distance(safePos, bombPos);
        
//         // Aumentou distância mínima de segurança
//         if (distance > 3.5f)
//         {
//             return true;
//         }
        
//         // Para posições mais próximas, verifica se há obstáculos bloqueando a explosão
//         Vector2 direction = (safePos - bombPos).normalized;
        
//         // Verifica tanto na horizontal quanto na vertical se está na linha de explosão
//         bool inHorizontalLine = Mathf.Abs(safePos.y - bombPos.y) < 0.5f;
//         bool inVerticalLine = Mathf.Abs(safePos.x - bombPos.x) < 0.5f;
        
//         if (!inHorizontalLine && !inVerticalLine)
//         {
//             return true; // Não está na linha de explosão
//         }
        
//         // Se está na linha de explosão, precisa ter obstáculo bloqueando
//         RaycastHit2D hit = Physics2D.Raycast(bombPos, direction, distance, obstacleMask);
//         return hit.collider != null;
//     }

//     private bool IsSafePosition(Vector2 position)
//     {
//         if (Physics2D.OverlapCircle(position, 0.5f, dangerMask))
//             return false;

//         Collider2D[] nearbyBombs = Physics2D.OverlapCircleAll(position, 3f, LayerMask.GetMask("Bomb"));
//         foreach (var bomb in nearbyBombs)
//         {
//             Bomb bombScript = bomb.GetComponent<Bomb>();
//             if (bombScript != null && bombScript.fuseTime < 1.5f)
//             {
//                 float distance = Vector2.Distance(position, bomb.transform.position);
//                 if (distance <= bombScript.explosionRadius + 0.5f)
//                 {
//                     RaycastHit2D hit = Physics2D.Raycast(position, 
//                         (bomb.transform.position - (Vector3)position).normalized, 
//                         distance, obstacleMask);
                    
//                     if (hit.collider == null)
//                         return false;
//                 }
//             }
//         }
        
//         return true;
//     }

//     private bool IsSafeToMoveTo(Vector2 direction)
//     {
//         if (direction == Vector2.zero) return true;
        
//         Vector2 targetPos = (Vector2)transform.position + direction * 2f;
//         return IsSafePosition(targetPos) && CanMove(direction);
//     }

//     private bool CanMove(Vector2 dir)
//     {
//         if (dir == Vector2.zero) return true;
        
//         RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, 0.6f, obstacleMask);
//         if (hit.collider != null) return false;
        
//         // MODIFICAÇÃO IMPORTANTE: Não evita a própria bomba durante os primeiros 0.5s após colocar
//         RaycastHit2D bombHit = Physics2D.Raycast(transform.position, dir, 0.6f, LayerMask.GetMask("Bomb"));
//         if (bombHit.collider != null)
//         {
//             // Se acabou de colocar a bomba, permite passar sobre ela brevemente
//             if (Vector2.Distance(bombHit.point, lastBombPosition) <= 0.8f && 
//                 Time.time - bombPlacedTime < 0.5f)
//             {
//                 Debug.Log("Permitindo passar sobre a própria bomba para escapar");
//                 return true;
//             }
//             return false;
//         }
        
//         return true;
//     }

//     private bool IsInDanger()
//     {
//         Collider2D hit = Physics2D.OverlapCircle(transform.position, 0.5f, dangerMask);
//         return hit != null;
//     }

//     private Vector2 FindSafeDirection()
//     {
//         List<Vector2> dirs = new List<Vector2>() { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
//         List<Vector2> safeDirs = new List<Vector2>();
        
//         foreach (var dir in dirs)
//         {
//             if (CanMove(dir))
//             {
//                 Vector2 newPos = (Vector2)transform.position + dir * 2f;
//                 if (IsSafePosition(newPos))
//                 {
//                     safeDirs.Add(dir);
//                     Debug.Log($"Direção segura encontrada: {dir}");
//                 }
//             }
//         }
        
//         if (safeDirs.Count > 0)
//         {
//             if (lastBombPosition != Vector2.zero)
//             {
//                 Vector2 awayFromBomb = ((Vector2)transform.position - lastBombPosition).normalized;
//                 float bestDot = -2f;
//                 Vector2 bestDir = safeDirs[0];
                
//                 foreach (var dir in safeDirs)
//                 {
//                     float dot = Vector2.Dot(dir, awayFromBomb);
//                     if (dot > bestDot)
//                     {
//                         bestDot = dot;
//                         bestDir = dir;
//                     }
//                 }
//                 return bestDir;
//             }
//             return safeDirs[Random.Range(0, safeDirs.Count)];
//         }
        
//         return GetRandomValidDirection();
//     }

//     private Vector2 GetRandomValidDirection()
//     {
//         List<Vector2> validDirs = new List<Vector2>();
//         Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        
//         foreach (var dir in dirs)
//         {
//             if (CanMove(dir))
//             {
//                 validDirs.Add(dir);
//             }
//         }
        
//         if (validDirs.Count > 0)
//         {
//             return validDirs[Random.Range(0, validDirs.Count)];
//         }
        
//         return Vector2.zero;
//     }

//     private GameObject FindNearest(LayerMask mask, float radius)
//     {
//         Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, mask);
//         GameObject nearest = null;
//         float minDist = Mathf.Infinity;

//         foreach (var hit in hits)
//         {
//             if (hit.gameObject == gameObject) continue;
            
//             float dist = Vector2.Distance(transform.position, hit.transform.position);
//             if (dist < minDist)
//             {
//                 minDist = dist;
//                 nearest = hit.gameObject;
//             }
//         }
//         return nearest;
//     }

//     private GameObject FindNearestDestructible()
//     {
//         GameObject destructibleTilemapObj = GameObject.FindGameObjectWithTag("Destructible");
//         if (destructibleTilemapObj == null)
//         {
//             return null;
//         }
        
//         Tilemap destructibleTilemap = destructibleTilemapObj.GetComponent<Tilemap>();
//         if (destructibleTilemap == null)
//         {
//             return null;
//         }
        
//         GameObject nearestTileMarker = null;
//         float minDist = Mathf.Infinity;
        
//         BoundsInt bounds = destructibleTilemap.cellBounds;
        
//         for (int x = bounds.xMin; x < bounds.xMax; x++)
//         {
//             for (int y = bounds.yMin; y < bounds.yMax; y++)
//             {
//                 Vector3Int cell = new Vector3Int(x, y, 0);
//                 if (destructibleTilemap.GetTile(cell) != null)
//                 {
//                     Vector3 worldPos = destructibleTilemap.GetCellCenterWorld(cell);
//                     float dist = Vector2.Distance(transform.position, worldPos);
                    
//                     if (dist < minDist && dist <= destructibleSearchRadius)
//                     {
//                         minDist = dist;
                        
//                         if (nearestTileMarker == null)
//                         {
//                             nearestTileMarker = new GameObject("NearestDestructibleMarker");
//                         }
                        
//                         nearestTileMarker.transform.position = worldPos;
//                     }
//                 }
//             }
//         }
        
//         return nearestTileMarker;
//     }

//     private Vector2 GetDirectionTo(Vector2 target)
//     {
//         Vector2 diff = target - (Vector2)transform.position;
        
//         if (Mathf.Abs(diff.x) <= 0.1f && Mathf.Abs(diff.y) <= 0.1f)
//         {
//             return Vector2.zero;
//         }
        
//         Vector2 preferredDirection = Vector2.zero;
//         if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y))
//         {
//             preferredDirection = diff.x > 0 ? Vector2.right : Vector2.left;
//         }
//         else
//         {
//             preferredDirection = diff.y > 0 ? Vector2.up : Vector2.down;
//         }
        
//         if (CanMove(preferredDirection))
//         {
//             return preferredDirection;
//         }
        
//         Vector2 alternativeDirection = Vector2.zero;
//         if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y))
//         {
//             alternativeDirection = diff.y > 0 ? Vector2.up : Vector2.down;
//         }
//         else
//         {
//             alternativeDirection = diff.x > 0 ? Vector2.right : Vector2.left;
//         }
        
//         if (CanMove(alternativeDirection))
//         {
//             return alternativeDirection;
//         }
        
//         return GetRandomValidDirection();
//     }

//     private Vector2 GetExplorationDirection()
//     {
//         List<Vector2> dirs = new List<Vector2>() { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
//         List<Vector2> validDirs = new List<Vector2>();
        
//         foreach (var dir in dirs)
//         {
//             if (CanMove(dir) && IsSafeToMoveTo(dir))
//             {
//                 validDirs.Add(dir);
//             }
//         }
        
//         if (validDirs.Count > 0)
//         {
//             return validDirs[Random.Range(0, validDirs.Count)];
//         }
        
//         return Vector2.zero;
//     }
// }