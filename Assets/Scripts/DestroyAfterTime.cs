using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    public float lifeTime = 2.0f; // Thời gian tồn tại (giây)

    void Start()
    {
        Destroy(gameObject, lifeTime); // Hủy GameObject sau khoảng thời gian lifeTime
    }
}