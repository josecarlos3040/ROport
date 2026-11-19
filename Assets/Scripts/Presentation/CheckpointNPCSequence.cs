using UnityEngine;

public class CheckpointNPCSequence : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private ROportRunManager runManager;

    [Header("NPCs na ordem dos checkpoints")]
    [SerializeField]
    private CheckpointNPCReaction[] checkpointNPCs;

    [Header("Progress")]
    [SerializeField]
    private int initialCheckpointIndex = 0;

    private int currentIndex;

    private void Awake()
    {
        if (runManager == null)
        {
            runManager =
                FindAnyObjectByType<ROportRunManager>();
        }

        ResetSequence();
    }

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
        if (checkpointNPCs == null ||
            currentIndex >= checkpointNPCs.Length)
            return;

        CheckpointNPCReaction npc =
            checkpointNPCs[currentIndex];

        if (npc != null)
            npc.CompleteDelivery();

        currentIndex++;

        if (currentIndex < checkpointNPCs.Length)
        {
            CheckpointNPCReaction nextNPC =
                checkpointNPCs[currentIndex];

            if (nextNPC != null)
                nextNPC.SetAvailable(true);
        }
    }

    public void ResetSequence()
    {
        if (checkpointNPCs == null)
            return;

        foreach (CheckpointNPCReaction npc
                 in checkpointNPCs)
        {
            if (npc != null)
                npc.ResetNPC();
        }

        currentIndex = Mathf.Clamp(
            initialCheckpointIndex,
            0,
            checkpointNPCs.Length);

        if (currentIndex < checkpointNPCs.Length)
        {
            if (checkpointNPCs[currentIndex] != null)
            {
                checkpointNPCs[currentIndex]
                    .SetAvailable(true);
            }
        }
    }
}