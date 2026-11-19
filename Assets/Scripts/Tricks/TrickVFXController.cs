using UnityEngine;

public class TrickVFXController : MonoBehaviour
{
    [Header("VFX Prefabs")]
    [SerializeField] private GameObject trickPrefab;
    [SerializeField] private GameObject hitPrefab;

    [Header("Spawn")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Vector3 spawnOffset;

    [Header("Durations")]
    [SerializeField, Min(0.1f)]
    private float hitLifetime = 2f;

    [SerializeField, Min(0.1f)]
    private float trickCleanupTime = 2f;

    [SerializeField, Min(0f)]
    private float hitCooldown = 0.3f;

    private GameObject activeTrick;
    private float lastHitTime = -999f;

    private void Awake()
    {
        if (spawnPoint == null)
            spawnPoint = transform;
    }

    // Acertou a manobra.
    public void PlaySuccess()
    {
        if (trickPrefab == null)
            return;

        StopSuccess();

        Vector3 position = GetSpawnPosition();

        // Acompanha o jogador durante a manobra.
        activeTrick = Instantiate(
            trickPrefab,
            position,
            spawnPoint.rotation,
            spawnPoint);

        StartParticles(activeTrick);
    }

    // Terminou de percorrer a zona.
    public void StopSuccess()
    {
        if (activeTrick == null)
            return;

        // Para de emitir, preservando as particulas
        // ja criadas ate desaparecerem.
        ParticleSystem[] systems =
            activeTrick.GetComponentsInChildren
                <ParticleSystem>(true);

        foreach (ParticleSystem system in systems)
        {
            system.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting);
        }

        // Solta o efeito do jogador para deixar
        // as particulas existentes terminarem.
        activeTrick.transform.SetParent(null, true);

        Destroy(activeTrick, trickCleanupTime);
        activeTrick = null;
    }

    // Errou a manobra ou bateu num obstaculo Trick.
    public void PlayFailure()
    {
        if (Time.time - lastHitTime < hitCooldown)
            return;

        lastHitTime = Time.time;

        StopSuccess();

        if (hitPrefab == null)
            return;

        GameObject hit = Instantiate(
            hitPrefab,
            GetSpawnPosition(),
            spawnPoint.rotation);

        StartParticles(hit);

        Destroy(hit, hitLifetime);
    }

    private Vector3 GetSpawnPosition()
    {
        return spawnPoint.position +
            spawnPoint.TransformDirection(spawnOffset);
    }

    private void StartParticles(GameObject effect)
    {
        if (!effect.activeSelf)
            effect.SetActive(true);

        ParticleSystem[] systems =
            effect.GetComponentsInChildren
                <ParticleSystem>(true);

        foreach (ParticleSystem system in systems)
        {
            system.Play();
        }
    }

    private void OnDisable()
    {
        StopSuccess();
    }

    [ContextMenu("ROport - Testar VFX Trick")]
    private void TestSuccess()
    {
        if (Application.isPlaying)
            PlaySuccess();
    }

    [ContextMenu("ROport - Testar VFX Hit")]
    private void TestFailure()
    {
        if (Application.isPlaying)
            PlayFailure();
    }
}