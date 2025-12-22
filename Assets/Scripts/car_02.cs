using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class car_02 : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform[] waypoints;
    public bool loop = true;

    [Header("Movement Settings")]
    public float speed = 5f;
    public float turnSpeed = 5f;

    [Header("Obstacle Detection")]
    public float detectDistance = 6f;            // 감지 거리
    public LayerMask obstacleLayer;              // 감지 레이어
    public bool showRay = true;                  // 레이 시각화 여부
    public Vector3 rayOffset = new Vector3(0, 0.3f, 0); // Ray 시작 위치 오프셋

    [Header("Horn Settings")]
    public AudioClip[] hornClips;             // 경적 소리 파일들
    public float minPatience = 2f;            // 경적 울리기 전 최소 대기 시간
    public float maxPatience = 5f;            // 경적 울리기 전 최대 대기 시간
    [Range(0.8f, 1.2f)]
    public float pitchVariation = 0.1f;       // 소리 톤 변화

    private int index = 0;
    private float groundY;
    private bool isStopped = false;

    // 경적 관련 변수
    private AudioSource audioSource;
    private float currentPatienceCounter;

    void Start()
    {
        groundY = transform.position.y;
        
        // 오디오 초기화
        audioSource = GetComponent<AudioSource>();
        ResetPatience();
    }

    void Update()
    {
        // 장애물 감지
        CheckObstacle();

        // 감지 중이면 이동 중단 및 경적 로직
        if (isStopped)
        {
            HandleTrafficJam();
            return;
        }
        else
        {
            ResetPatience(); // 이동 중엔 인내심 리셋
        }

        if (waypoints == null || waypoints.Length == 0)
            return;

        Transform target = waypoints[index];

        // 1) 목표까지 수평 방향
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        // 웨이포인트 도달 시 다음으로
        if (toTarget.sqrMagnitude < 0.05f)
        {
            index++;
            if (index >= waypoints.Length)
            {
                if (loop) index = 0;
                else return;
            }
            return;
        }

        Vector3 dir = toTarget.normalized;

        // 2) 회전: forward(Z+)가 dir을 향하게
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            turnSpeed * Time.deltaTime
        );

        // 3) 이동: 항상 forward(Z+)
        Vector3 forwardFlat = transform.forward;
        forwardFlat.y = 0f;
        forwardFlat.Normalize();

        transform.position += forwardFlat * speed * Time.deltaTime;

        // 4) Y 고정
        transform.position = new Vector3(
            transform.position.x,
            groundY,
            transform.position.z
        );
    }

    // -------------------------------------------------------------
    // 장애물 감지 시스템
    // -------------------------------------------------------------
    private void CheckObstacle()
    {
        // Ray 시작점
        Vector3 start = transform.position + transform.TransformDirection(rayOffset);

        // 방향 : car_02의 앞은 transform.forward
        Vector3 direction = transform.forward;

        if (showRay)
        {
            Debug.DrawRay(start, direction * detectDistance, Color.red);
        }

        // 앞 방향으로 detectDistance 만큼 Raycast
        if (Physics.Raycast(start, direction, out RaycastHit hit, detectDistance, obstacleLayer))
        {
            isStopped = true;   // 감지 → 멈춤
        }
        else
        {
            isStopped = false;  // 없음 → 이동
        }
    }

    // -----------------------------------------
    // 경적 관련 로직
    // -----------------------------------------
    void HandleTrafficJam()
    {
        currentPatienceCounter -= Time.deltaTime;

        if (currentPatienceCounter <= 0)
        {
            HonkHorn();
            ResetPatience();
        }
    }

    void HonkHorn()
    {
        if (hornClips == null || hornClips.Length == 0) return;

        int randomIndex = Random.Range(0, hornClips.Length);
        audioSource.pitch = Random.Range(1.0f - pitchVariation, 1.0f + pitchVariation);
        audioSource.PlayOneShot(hornClips[randomIndex]);
    }

    void ResetPatience()
    {
        currentPatienceCounter = Random.Range(minPatience, maxPatience);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
    }
}