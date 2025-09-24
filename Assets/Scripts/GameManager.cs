using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic; // Necessário para usar Listas

[DefaultExecutionOrder(-1)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Trocamos o array fixo por uma LISTA dinâmica.
    // Ela pode crescer e encolher conforme os jogadores vivem e morrem.
    private List<GameObject> alivePlayers = new List<GameObject>();

    // Mantemos uma referência separada para os bots para facilitar a notificação
    private List<BotController> activeBots = new List<BotController>();

    private void Awake()
    {
        if (Instance != null) {
            DestroyImmediate(gameObject);
        } else {
            Instance = this;
            // Garante que o GameManager não seja destruído ao recarregar a cena
            DontDestroyOnLoad(gameObject);
        }
    }

    // O Start() não precisa mais encontrar os jogadores.
    // Eles irão se registrar sozinhos.
    private void Start()
    {
        // Limpa as listas caso a cena seja recarregada
        alivePlayers.Clear();
        activeBots.Clear();
    }
    
    // NOVO: Método para os personagens se registrarem
    public void RegisterCharacter(GameObject character)
    {
        if (!alivePlayers.Contains(character))
        {
            alivePlayers.Add(character);

            // Se for um bot, guarda na lista de bots também
            BotController bot = character.GetComponent<BotController>();
            if (bot != null && !activeBots.Contains(bot))
            {
                activeBots.Add(bot);
            }
        }
    }

    // NOVO: Método central para gerenciar a morte
    public void CharacterDied(GameObject deadCharacter)
    {
        // 1. Remove o personagem morto da lista de vivos
        if (alivePlayers.Contains(deadCharacter))
        {
            alivePlayers.Remove(deadCharacter);
        }
        
        BotController deadBot = deadCharacter.GetComponent<BotController>();
        if (deadBot != null && activeBots.Contains(deadBot))
        {
            activeBots.Remove(deadBot);
        }

        // 2. Notifica todos os bots SOBREVIVENTES para que removam o alvo
        foreach (BotController survivingBot in activeBots)
        {
            // O bot para de mirar no personagem morto, evitando erros
            survivingBot.RemoveTarget(deadCharacter.transform);
        }
        
        // 3. Após a limpeza, a verificação de vitória é chamada
        CheckWinState();
    }


    public void CheckWinState()
    {
        // A lógica fica muito mais simples e segura: basta checar o tamanho da lista.
        if (alivePlayers.Count <= 1) {
            Invoke(nameof(NewRound), 3f);
        }
    }

    private void NewRound()
    {
        // Recarrega a cena para uma nova rodada
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        if (Instance == this) {
            Instance = null;
        }
    }
}