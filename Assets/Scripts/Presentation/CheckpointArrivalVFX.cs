using UnityEngine;

public class CheckpointArrivalVFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private ROportRunManager runManager;

    [SerializeField]
    private Transform player;

    [Header("VFX")]
    [SerializeField]
    private GameObject checkpointVFXPrefab;

    [SerializeField]
    private Vector3 spawnOffset =
        new Vector3(0f, 0.5f, 0f);

    [SerializeField, Min(0.1f)]
    private float effectDuration = 5f;

    private void OnEnable()
    {
        if (runManager != null)
        {
            runManager.CheckpointReached +=
                OnCheckpointReached;
        }
    }

    private void OnDisable()
    {
        if (runManager != null)
        {
            runManager.CheckpointReached -=
                OnCheckpointReached;
        }
    }

    private void OnCheckpointReached()
    {
        if (checkpointVFXPrefab == null ||
            player == null)
        {
            Debug.LogWarning(
                "[ROport VFX] Falta configurar " +
                "o prefab ou o Player.");
            return;
        }

        Vector3 spawnPosition =
            player.position + spawnOffset;

        GameObject effect = Instantiate(
            checkpointVFXPrefab,
            spawnPosition,
            checkpointVFXPrefab.transform.rotation);

        // Inicia todos os emissores de particulas.
        ParticleSystem[] particles =
            effect.GetComponentsInChildren<ParticleSystem>();

        foreach (ParticleSystem particle in particles)
        {
            if (particle != null)
                particle.Play();
        }

        Destroy(effect, effectDuration);

        Debug.Log(
            "[ROport VFX] Entrega concluida!");
    }
}