using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Bomb : MonoBehaviour
{
    private float fuseTime;
    private int explosionRadius;
    private float explosionDuration;
    private Explosion explosionPrefab;
    private LayerMask explosionLayerMask;
    private Tilemap destructibleTiles;
    private Destructible destructiblePrefab;

    private Rigidbody2D rb;
    private bool isMoving;
    private Vector2 moveDirection;
    public float moveSpeed = 100f;

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

        StartCoroutine(ExplodeAfterDelay());
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
        position.x = Mathf.Round(position.x);
        position.y = Mathf.Round(position.y);

        Explosion centerExplosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        centerExplosion.SetActiveRenderer(centerExplosion.start);
        centerExplosion.DestroyAfter(explosionDuration);

        DoExplode(position, Vector2.up, explosionRadius);
        DoExplode(position, Vector2.down, explosionRadius);
        DoExplode(position, Vector2.left, explosionRadius);
        DoExplode(position, Vector2.right, explosionRadius);

        Destroy(gameObject);
    }

    private void DoExplode(Vector2 position, Vector2 direction, int length)
    {
        if (length <= 0) return;

        position += direction;

        if (Physics2D.OverlapBox(position, Vector2.one / 2f, 0f, explosionLayerMask))
        {
            ClearDestructible(position);
            return;
        }

        Explosion explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        explosion.SetActiveRenderer(length > 1 ? explosion.middle : explosion.end);
        explosion.SetDirection(direction);
        explosion.DestroyAfter(explosionDuration);

        DoExplode(position, direction, length - 1);
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
