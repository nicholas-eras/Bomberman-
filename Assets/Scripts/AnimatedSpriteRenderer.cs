using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AnimatedSpriteRenderer : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    public Sprite idleSprite;
    public Sprite[] animationSprites;

    public float animationTime = 0.25f;
    private int animationFrame;

    public bool loop = true;

    // === ADICIONADO: Propriedade para calcular a duração total ===
    public float TotalAnimationDuration
    {
        get { return animationSprites.Length * animationTime; }
    }
    // ==========================================================

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        spriteRenderer.enabled = true;
        CancelInvoke();
        InvokeRepeating(nameof(NextFrame), animationTime, animationTime);
    }

    private void OnDisable()
    {
        spriteRenderer.enabled = false;
        CancelInvoke();
    }

    private void NextFrame()
    {
        if (animationSprites == null || animationSprites.Length == 0) return;

        animationFrame++;

        if (loop && animationFrame >= animationSprites.Length) {
            animationFrame = 0;
        }

        if (animationFrame >= 0 && animationFrame < animationSprites.Length) {
            spriteRenderer.sprite = animationSprites[animationFrame];
        }
    }
    
    public void RestartAnimation()
    {
        animationFrame = -1;
        NextFrame();
    }

    // NOVO MÉTODO: Para o controlador definir o sprite de 'idle'
    public void SetIdleSprite()
    {
        if (idleSprite != null) {
            spriteRenderer.sprite = idleSprite;
        }
        CancelInvoke();
    }
}