using System;
using TMPro;
using UnityEngine;

public class TrickScoreManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private ROportRunManager runManager;

    [SerializeField]
    private CityProgressManager cityProgressManager;

    [Header("UI")]
    [SerializeField]
    private TMP_Text scoreText;

    [Header("Photo Points")]
    [SerializeField, Min(0)]
    private int photoPoints = 50;

    public int CurrentScore { get; private set; }

    public event Action<int> ScoreChanged;

    private void Awake()
    {
        UpdateUI();
    }

    private void OnEnable()
    {
        if (cityProgressManager != null)
        {
            cityProgressManager.PhotoRecorded +=
                OnPhotoRecorded;
        }

        UpdateUI();
    }

    private void OnDisable()
    {
        if (cityProgressManager != null)
        {
            cityProgressManager.PhotoRecorded -=
                OnPhotoRecorded;
        }
    }

    private void OnPhotoRecorded(CityProblem problem)
    {
        if (problem == null)
            return;

        AddPoints(photoPoints);

        Debug.Log(
            "[ROport] Foto de " +
            problem.DisplayName +
            ": +" + photoPoints + " pontos");
    }

    public bool RegisterTrick(int points)
    {
        if (runManager == null ||
            runManager.Phase != RunPhase.Executing ||
            points <= 0)
        {
            return false;
        }

        AddPoints(points);

        Debug.Log(
            "[ROport] Manobra realizada: +" +
            points + " pontos");

        return true;
    }

    private void AddPoints(int amount)
    {
        if (amount <= 0)
            return;

        CurrentScore += amount;

        UpdateUI();
        ScoreChanged?.Invoke(CurrentScore);
    }

    // Usar somente ao comecar uma nova partida
    // completa, nunca entre checkpoints.
    public void ResetScoreForNewGame()
    {
        CurrentScore = 0;

        UpdateUI();
        ScoreChanged?.Invoke(CurrentScore);

        Debug.Log(
            "[ROport] Pontuacao reiniciada " +
            "para uma nova partida.");
    }

    private void UpdateUI()
    {
        if (scoreText != null)
        {
            scoreText.text =
                "PONTOS: " + CurrentScore;
        }
    }
}