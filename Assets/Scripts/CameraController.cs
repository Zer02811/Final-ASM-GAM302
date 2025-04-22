using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;

    private void LateUpdate()
    {
        if(target == null)
            return;
        transform.position = Vector3.Lerp(transform.position, target.position, 0.1f);
    }
}
