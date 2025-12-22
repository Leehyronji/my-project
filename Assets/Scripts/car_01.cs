using UnityEngine;

[RequireComponent(typeof(AudioSource))] // AudioSource 컴포넌트 자동 추가
public class car_01 : MonoBehaviour
{
    [Header("Path Settings")]
    public Transform[] waypoints;
    public bool loop = true;

    [Header("Movement Settings")]
    public float speed = 5f;
    public float turnSpeed = 5f;

    [Header("Obstacle Detection")]
    public float detectDistance = 6f;         // 감지 거리
    public LayerMask obstacleLayer;           // 감지할 레이어
    public bool showRay = true;               // Scene에서 레이 시각화

    [Header("Horn Settings (New)")]
    public AudioClip[] hornClips;             // 경적 소리 파일들
    public float minPatience = 2f;            // 경적 울리기 전 최소 대기 시간
    public float maxPatience = 5f;            // 경적 울리기 전 최대 대기 시간
    [Range(0.8f, 1.2f)]
    public float pitchVariation = 0.1f;       // 소리 톤 변화 (자연스러움 추가)

    private int currentIndex = 0;
    private bool isStopped = false;           // 감지 중 멈춤
    
    // 경적 관련 변수
    private AudioSource audioSource;
    private float currentPatienceCounter;

    // 이 차량에서 "앞"은 빨간색 축(X)의 -방향
    private Vector3 ModelForward => -transform.right;

    void Start()
    {
        // 오디오 소스 가져오기 및 초기화
        audioSource = GetComponent<AudioSource>();
        ResetPatience();
    }

    void Update()
    {
        // 장애물 감지 & 멈춤 판단
        CheckObstacle();

        // 멈춰있는 중이면 이동하지 않음 + 경적 로직 실행
        if (isStopped)
        {
            HandleTrafficJam(); // [추가됨] 정체 시 경적 로직
            return;
        }
        else
        {
            // 움직이는 중이면 인내심 리셋 (차가 빠지면 다시 참음)
            ResetPatience(); 
        }

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

        // 2) 회전: 현재 "앞"을 목표 방향에 맞추기
        Quaternion from = Quaternion.LookRotation(ModelForward, Vector3.up);
        Quaternion to = Quaternion.LookRotation(desiredDir, Vector3.up);
        Quaternion delta = to * Quaternion.Inverse(from);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            delta * transform.rotation,
            turnSpeed * Time.deltaTime
        );

        // 3) 이동
        transform.position += ModelForward * speed * Time.deltaTime;
    }

    // -----------------------------------------
    // 장애물 감지
    // -----------------------------------------
    private void CheckObstacle()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 0.3f, ModelForward);
        
        // 앞 방향으로 detectDistance 만큼 Raycast
        if (Physics.Raycast(ray, out RaycastHit hit, detectDistance, obstacleLayer))
        {
            isStopped = true;   // 감지 → 멈춤
        }
        else
        {
            isStopped = false;  // 없음 → 다시 이동
        }
    }

    // -----------------------------------------
    // [추가됨] 경적 관련 로직
    // -----------------------------------------
    void HandleTrafficJam()
    {
        // 멈춰있는 동안 시간 카운트 다운
        currentPatienceCounter -= Time.deltaTime;

        // 인내심이 바닥나면 경적 울리기
        if (currentPatienceCounter <= 0)
        {
            HonkHorn();
            ResetPatience(); // 경적 울린 후 다시 타이머 리셋
        }
    }

    void HonkHorn()
    {
        if (hornClips == null || hornClips.Length == 0) return;

        // 소리가 겹치지 않게 하려면 아래 주석 해제 (지금은 겹쳐서 시끄러운게 더 리얼할 수 있음)
        // if (audioSource.isPlaying) return; 

        // 1. 랜덤한 경적 소리 선택
        int randomIndex = Random.Range(0, hornClips.Length);
        
        // 2. 소리 톤(Pitch)을 살짝 랜덤 조절
        audioSource.pitch = Random.Range(1.0f - pitchVariation, 1.0f + pitchVariation);
        
        // 3. 소리 재생
        audioSource.PlayOneShot(hornClips[randomIndex]);
    }

    void ResetPatience()
    {
        // 다음 경적까지 걸리는 시간을 랜덤하게 재설정 (예: 2초 ~ 5초 사이)
        currentPatienceCounter = Random.Range(minPatience, maxPatience);
    }

    // -----------------------------------------
    // 디버그 시각화
    // -----------------------------------------
    void OnDrawGizmos()
    {
        if (!showRay) return;
        Gizmos.color = Color.red;

        Vector3 start = transform.position + Vector3.up * 0.3f;
        Gizmos.DrawLine(start, start + ModelForward * detectDistance);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        // ModelForward 프로퍼티 활용
        Gizmos.DrawLine(transform.position, transform.position + ModelForward * 2f);
    }
}