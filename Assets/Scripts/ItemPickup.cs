using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public enum ItemType
    {
        ExtraBomb,
        BlastRadius,
        SpeedIncrease,
        KickBomb,
    }

    public ItemType type;

    private void OnItemPickup(GameObject entity)
    {
        // Diferencia por componente: se tem BotController é bot, senão é player
        BotController botController = entity.GetComponent<BotController>();
        
        if (botController != null)
        {
            HandleBotPickup(entity);
        }
        else
        {
            HandlePlayerPickup(entity);
        }

        Destroy(gameObject);
    }

    private void HandlePlayerPickup(GameObject player)
    {
        switch (type)
        {
            case ItemType.ExtraBomb:
                var bombController = player.GetComponent<BombController>();
                if (bombController != null)
                    bombController.AddBomb();
                break;

            case ItemType.BlastRadius:
                var bombCtrl = player.GetComponent<BombController>();
                if (bombCtrl != null)
                    bombCtrl.explosionRadius++;
                break;

            case ItemType.SpeedIncrease:
                var movementController = player.GetComponent<MovementController>();
                if (movementController != null)
                    movementController.speed += 0.5f;
                break;

            case ItemType.KickBomb:
                var moveCtrl = player.GetComponent<MovementController>();
                if (moveCtrl != null)
                    moveCtrl.canKickBomb = true;
                break;
        }
    }

    private void HandleBotPickup(GameObject bot)
    {
        BotController botController = bot.GetComponent<BotController>();
        if (botController == null) return;

        switch (type)
        {
            case ItemType.ExtraBomb:
                botController.bombAmount++;
                botController.AddBombToStock();
                break;

            case ItemType.BlastRadius:                
                botController.explosionRadius++;
                break;

            case ItemType.SpeedIncrease:
                botController.speed += 0.5f;
                break;

            case ItemType.KickBomb:
                botController.canKickBomb = true;
                break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            OnItemPickup(other.gameObject);
        }
    }
}