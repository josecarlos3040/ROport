using UnityEngine;

public class PhoneBubbleFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private RectTransform bubbleRect;
    [SerializeField] private Transform player;

    [Header("Anchor")]
    [SerializeField] private Vector3 worldAnchorOffset = new Vector3(0f, 1.1f, 0f);

    [Header("Screen Offset")]
    [SerializeField] private Vector2 screenOffset = new Vector2(220f, 10f);

    [Header("Clamp")]
    [SerializeField] private Vector2 clampPadding = new Vector2(80f, 80f);

    private void LateUpdate()
    {
        if (targetCamera == null || canvasRect == null || bubbleRect == null || player == null)
            return;

        Vector3 worldPoint = player.position + worldAnchorOffset;
        Vector3 screenPoint = targetCamera.WorldToScreenPoint(worldPoint);

        if (screenPoint.z < 0f)
        {
            bubbleRect.gameObject.SetActive(false);
            return;
        }

        bubbleRect.gameObject.SetActive(true);

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            null,
            out localPoint);

        localPoint += screenOffset;

        Vector2 halfCanvas = canvasRect.rect.size * 0.5f;
        Vector2 halfBubble = bubbleRect.rect.size * 0.5f;

        float minX = -halfCanvas.x + halfBubble.x + clampPadding.x;
        float maxX = halfCanvas.x - halfBubble.x - clampPadding.x;
        float minY = -halfCanvas.y + halfBubble.y + clampPadding.y;
        float maxY = halfCanvas.y - halfBubble.y - clampPadding.y;

        localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
        localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

        bubbleRect.anchoredPosition = localPoint;
    }
}