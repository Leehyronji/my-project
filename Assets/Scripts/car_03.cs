using UnityEngine;

public class car_03 : MonoBehaviour
{
    [Header("Path Settings")]
    public Transform[] waypoints;
    public bool loop = true;

    [Header("Movement Settings")]
    public float speed = 5f;
    public float turnSpeed = 5f;

    private int currentIndex = 0;

    // 이 차량에서 "앞"은 빨간색 축(X)의 -방향
    private Vector3 ModelForward
    {
        get { return -transform.right; }
    }

    void Update()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        Transform target = waypoints[currentIndex];

        // 1) 목표 지점까지의 방향 (수평면에서만)
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        // 웨이포인트 도달 체크
        if (toTarget.sqrMagnitude < 0.05f)
        {
            currentIndex++;
            if (currentIndex >= waypoints.Length)
            {
                if (loop) currentIndex = 0;
                else return;
            }
            return;
        }

        Vector3 desiredDir = toTarget.normalized;

        // 2) 회전: 현재 "앞" (ModelForward)을 목표 방향(desiredDir)에 맞추기
        Quaternion from = Quaternion.LookRotation(ModelForward, Vector3.up);
        Quaternion to = Quaternion.LookRotation(desiredDir, Vector3.up);
        Quaternion delta = to * Quaternion.Inverse(from);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            delta * transform.rotation,
            turnSpeed * Time.deltaTime
        );

        // 3) 이동: 항상 빨간축의 -방향(= ModelForward)으로 전진
        transform.position += ModelForward * speed * Time.deltaTime;
    }

    // 디버그용: 씬에서 현재 "앞" 방향을 눈으로 확인
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + (-transform.right) * 2f);
    }
}