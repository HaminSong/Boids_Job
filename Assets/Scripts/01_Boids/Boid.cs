using UnityEngine;

public class Boid : MonoBehaviour
{
    [HideInInspector] public Vector3 velocity;

    public void UpdateBoid(Vector3 force, float minSpeed, float maxSpeed)
    {
        // 힘을 속도에 반영
        velocity += force * Time.deltaTime;

        // 속도 크기 제한
        float speed = velocity.magnitude;
        velocity = velocity.normalized * Mathf.Clamp(speed, minSpeed, maxSpeed);

        // 위치 업데이트
        transform.position += velocity * Time.deltaTime;

        // 이동 방향으로 회전
        if (velocity != Vector3.zero)
            transform.forward = velocity.normalized;
    }
}
