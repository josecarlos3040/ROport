using UnityEngine;

public class PhoneMapMarker : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float height = 8f;
    [SerializeField] private bool rotateWithPlayer = false;

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 pos = target.position;
        pos.y += height;
        transform.position = pos;

        if (rotateWithPlayer)
        {
            transform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}