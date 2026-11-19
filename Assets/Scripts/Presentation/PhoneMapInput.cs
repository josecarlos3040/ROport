using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(RawImage))]
public class PhoneMapInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RawImage mapImage;
    [SerializeField] private Camera phoneMapCamera;
    [SerializeField] private Canvas uiCanvas;

    private void Awake()
    {
        if (mapImage == null)
            mapImage = GetComponent<RawImage>();

        if (uiCanvas == null)
            uiCanvas = GetComponentInParent<Canvas>();
    }

    public bool IsPointerOverMap()
    {
        return TryGetMapUV(out _);
    }

    public bool TryGetMapRay(out Ray ray)
    {
        ray = default;

        if (phoneMapCamera == null ||
            !TryGetMapUV(out Vector2 uv))
        {
            return false;
        }

        ray = phoneMapCamera.ViewportPointToRay(
            new Vector3(uv.x, uv.y, 0f));

        return true;
    }

    private bool TryGetMapUV(out Vector2 uv)
    {
        uv = Vector2.zero;

        if (Mouse.current == null ||
            mapImage == null ||
            !mapImage.isActiveAndEnabled ||
            !mapImage.gameObject.activeInHierarchy)
        {
            return false;
        }

        RectTransform rectTransform =
            mapImage.rectTransform;

        Canvas rootCanvas = uiCanvas != null
            ? uiCanvas.rootCanvas
            : null;

        Camera uiCamera = null;

        if (rootCanvas != null &&
            rootCanvas.renderMode !=
            RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = rootCanvas.worldCamera;

            if (uiCamera == null)
                uiCamera = Camera.main;
        }

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        if (!RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                rectTransform,
                mousePosition,
                uiCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = rectTransform.rect;

        if (!rect.Contains(localPoint) ||
            rect.width <= 0f ||
            rect.height <= 0f)
        {
            return false;
        }

        // Coordenadas normalizadas dentro do RawImage.
        float u = Mathf.InverseLerp(
            rect.xMin, rect.xMax, localPoint.x);

        float v = Mathf.InverseLerp(
            rect.yMin, rect.yMax, localPoint.y);

        // Respeita ajustes de UV feitos no RawImage.
        Rect uvRect = mapImage.uvRect;

        uv = new Vector2(
            uvRect.x + u * uvRect.width,
            uvRect.y + v * uvRect.height);

        return true;
    }
}