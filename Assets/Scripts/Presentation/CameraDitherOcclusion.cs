using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(500)]
public class CameraDitherOcclusion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform player;
    [SerializeField] private ROportRunManager runManager;

    [Header("Activation")]
    [SerializeField] private bool onlyDuringExecution = true;

    [Header("Detection")]
    [SerializeField, Min(0f)]
    private float boundsPadding = 0.25f;

    [SerializeField, Range(0f, 400f)]
    private float screenMarginPixels = 120f;

    [SerializeField]
    private bool useScreenProximity = false;

    [SerializeField]
    private Vector3 playerTargetOffset =
        new Vector3(0f, 0.3f, 0f);

    [Header("Near Camera")]
    [SerializeField] private bool fadeCloseObjects = true;

    [SerializeField, Min(0.1f)]
    private float nearCameraRadius = 2f;

    [Header("Dither")]
    [SerializeField, Range(0f, 1f)]
    private float minimumVisibility = 0.25f;

    [SerializeField, Min(0.1f)]
    private float fadeSpeed = 3f;

    [Header("Debug")]
    [SerializeField] private bool forceDitherForTesting;

    private class OccluderData
    {
        public DitherOccluder occluder;
        public Renderer[] renderers;
    }

    private readonly List<OccluderData> occluders =
        new List<OccluderData>();

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Start()
    {
        RefreshOccluders();
    }

    [ContextMenu("ROport - Atualizar objetos Dither")]
    public void RefreshOccluders()
    {
        foreach (OccluderData data in occluders)
        {
            if (data.occluder != null)
                data.occluder.RestoreImmediate();
        }

        occluders.Clear();

        DitherOccluder[] found =
            FindObjectsByType<DitherOccluder>();

        foreach (DitherOccluder occluder in found)
        {
            occluders.Add(new OccluderData
            {
                occluder = occluder,
                renderers = occluder
                    .GetComponentsInChildren<Renderer>(true)
            });
        }

        Debug.Log(
            "[ROport Dither] Objetos registrados: " +
            occluders.Count);
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        bool canDetect =
            !onlyDuringExecution ||
            (runManager != null &&
             runManager.Phase == RunPhase.Executing);

        Vector3 cameraPosition =
            targetCamera.transform.position;

        Vector3 playerPosition = player != null
            ? player.position + playerTargetOffset
            : Vector3.zero;

        Vector3 difference =
            playerPosition - cameraPosition;

        float distance = difference.magnitude;

        Ray ray = new Ray(
            cameraPosition,
            distance > 0.001f
                ? difference / distance
                : targetCamera.transform.forward);

        Vector3 playerScreen =
            player != null
                ? targetCamera.WorldToScreenPoint(playerPosition)
                : Vector3.zero;

        foreach (OccluderData data in occluders)
        {
            if (data.occluder == null)
                continue;

            bool shouldFade = forceDitherForTesting;

            if (!shouldFade &&
                canDetect &&
                player != null &&
                playerScreen.z > 0f)
            {
                shouldFade = IsOccluding(
                    data,
                    ray,
                    distance,
                    cameraPosition,
                    playerScreen);
            }

            data.occluder.SetOccluded(
                shouldFade,
                minimumVisibility);

            data.occluder.UpdateFade(
                fadeSpeed,
                Time.deltaTime);
        }
    }

    private bool IsOccluding(
        OccluderData data,
        Ray ray,
        float maxDistance,
        Vector3 cameraPosition,
        Vector3 playerScreen)
    {
        foreach (Renderer renderer in data.renderers)
        {
            if (renderer == null ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                renderer is SpriteRenderer ||
                renderer is ParticleSystemRenderer)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            bounds.Expand(boundsPadding * 2f);

            if (fadeCloseObjects &&
                bounds.SqrDistance(cameraPosition) <=
                nearCameraRadius * nearCameraRadius)
            {
                return true;
            }

            if (bounds.Contains(cameraPosition))
                return true;

            // Deteccao original pela linha de visao.
            if (bounds.IntersectRay(ray, out float hitDistance) &&
                hitDistance >= 0f &&
                hitDistance <= maxDistance)
            {
                return true;
            }

            // Deteccao adicional por proximidade
            // com a posicao do Player na tela.
            if (useScreenProximity &&
                IsNearPlayerOnScreen(bounds, playerScreen))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsNearPlayerOnScreen(
        Bounds bounds,
        Vector3 playerScreen)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        Vector3 cameraPosition =
            targetCamera.transform.position;

        Vector3 forward =
            targetCamera.transform.forward;

        float centerDepth = Vector3.Dot(
            center - cameraPosition, forward);

        float depthRadius =
            Mathf.Abs(forward.x) * extents.x +
            Mathf.Abs(forward.y) * extents.y +
            Mathf.Abs(forward.z) * extents.z;

        // Ignora objetos totalmente atras da camera
        // ou totalmente atras do personagem.
        if (centerDepth + depthRadius <= 0f ||
            centerDepth - depthRadius >= playerScreen.z)
        {
            return false;
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        bool hasVisibleCorner = false;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = center + new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z);

            Vector3 screen =
                targetCamera.WorldToScreenPoint(corner);

            if (screen.z <= 0f)
                continue;

            hasVisibleCorner = true;

            minX = Mathf.Min(minX, screen.x);
            maxX = Mathf.Max(maxX, screen.x);
            minY = Mathf.Min(minY, screen.y);
            maxY = Mathf.Max(maxY, screen.y);
        }

        if (!hasVisibleCorner)
            return false;

        float padding = screenMarginPixels;

        return playerScreen.x >= minX - padding &&
               playerScreen.x <= maxX + padding &&
               playerScreen.y >= minY - padding &&
               playerScreen.y <= maxY + padding;
    }

    private void OnDisable()
    {
        foreach (OccluderData data in occluders)
        {
            if (data.occluder != null)
                data.occluder.RestoreImmediate();
        }

        occluders.Clear();
    }
}