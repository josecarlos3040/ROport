using System.Collections;
using UnityEngine;

public class CheckpointNPCReaction : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Sprites")]
    [SerializeField] private Sprite waitingSprite;
    [SerializeField] private Sprite happySprite;

    [Header("Animator - Optional")]
    [SerializeField] private bool useAnimator = false;
    [SerializeField] private Animator npcAnimator;
    [SerializeField] private string waitingState = "Standby";
    [SerializeField] private string happyState = "Happy";

    [Header("Delivery VFX")]
    [SerializeField] private GameObject deliveryVFXPrefab;
    [SerializeField] private Transform vfxSpawnPoint;
    [SerializeField] private float vfxLifetime = 5f;

    [Header("Reaction")]
    [SerializeField] private float happyDuration = 2.5f;

    private bool delivered;
    private bool available;
    private Coroutine hideCoroutine;

    public void ResetNPC()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        delivered = false;
        available = false;

        SetWaiting();

        if (visualRoot != null)
            visualRoot.SetActive(false);
        else if (spriteRenderer != null)
            spriteRenderer.enabled = false;
    }

    public void SetAvailable(bool value)
    {
        if (delivered)
            return;

        available = value;

        if (visualRoot != null)
            visualRoot.SetActive(value);
        else if (spriteRenderer != null)
            spriteRenderer.enabled = value;

        if (value)
            SetWaiting();
    }

    public void CompleteDelivery()
    {
        if (!available || delivered)
            return;

        delivered = true;
        available = false;

        if (visualRoot != null)
            visualRoot.SetActive(true);

        SetHappy();
        SpawnDeliveryVFX();

        hideCoroutine =
            StartCoroutine(HideAfterDelay());

        Debug.Log(
            "[ROport NPC] Entrega concluida: " + name);
    }

    private void SetWaiting()
    {
        if (useAnimator && npcAnimator != null)
        {
            npcAnimator.Play(waitingState, 0, 0f);
        }
        else if (spriteRenderer != null &&
                 waitingSprite != null)
        {
            spriteRenderer.sprite = waitingSprite;
        }
    }

    private void SetHappy()
    {
        if (useAnimator && npcAnimator != null)
        {
            npcAnimator.Play(happyState, 0, 0f);
        }
        else if (spriteRenderer != null &&
                 happySprite != null)
        {
            spriteRenderer.sprite = happySprite;
        }
    }

    private void SpawnDeliveryVFX()
    {
        if (deliveryVFXPrefab == null)
            return;

        Transform spawn =
            vfxSpawnPoint != null
                ? vfxSpawnPoint
                : transform;

        GameObject effect = Instantiate(
            deliveryVFXPrefab,
            spawn.position,
            spawn.rotation);

        ParticleSystem[] particles =
            effect.GetComponentsInChildren<ParticleSystem>(
                true);

        foreach (ParticleSystem particle in particles)
        {
            if (particle == null)
                continue;

            particle.gameObject.SetActive(true);
            particle.Play();
        }

        Destroy(effect, vfxLifetime);
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(
            happyDuration);

        if (visualRoot != null)
            visualRoot.SetActive(false);
        else if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        hideCoroutine = null;
    }
}