using System.Collections;                 // ★ 코루틴용
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class event_car : MonoBehaviour, ISensorReactiveEvent
{
    [Header("사람 탐색 설정")]
    public float radius = 15f;
    public LayerMask peopleLayer;

    [Header("★ 추가 엔딩 대상 레이어 (peopleLayer2 역할)")]
    [Tooltip("EventEnd1 + 잠깐 멈춤 + EventOff1 후 재개까지 함께 받을 추가 대상 레이어")]
    public LayerMask extraEndLayer;

    [Header("★ 3D 게이지 설정")]
    public GameObject discomfortGaugeObject;
    public Slider discomfortSlider;
    public Image sliderFillImage;
    public Color normalColor = Color.green;
    public Color warningColor = Color.red;

    [Header("★ 이모티콘 설정")]
    public GameObject angryEmojiPrefab;      
    public float angryEmojiSpawnDelay = 3f;
    public float maxDiscomfortDuration = 30f;

    [Header("엔딩 구역 설정")]
    public float endRadius = 3f;
    public LayerMask endingLayer;
    public string endTriggerName = "EventEnd1";
    public string endOffTriggerName = "EventOff1";  // ★ 10초 뒤에 보낼 트리거 이름

    [Header("엔딩 후 재개 설정")]
    public float resumeWalkDelay = 7f;   // ★ end 이벤트 후 다시 걷기 + EventOff까지 대기 시간(초)

    [Header("★ 엔딩 카운트 설정")]
    [Tooltip("Animator의 EndCount 파라미터 이름")]
    public string endCountParamName = "EndCount";

    private float globalDiscomfortTimer = 0f;
    private HashSet<Animator> insideAnimators = new HashSet<Animator>();
    private Dictionary<Animator, float> animatorAngryEmojiTimers = new Dictionary<Animator, float>();
    private Dictionary<Animator, List<GameObject>> spawnedEmojis = new Dictionary<Animator, List<GameObject>>();
    
    private bool problemSolved = false;
    private bool endingActivated = false;

    // ★ 이 존이 EndCount를 이미 한 번 올렸는지 여부
    private bool endCountGiven = false;

    void Start()
    {
        if (discomfortGaugeObject != null) 
            discomfortGaugeObject.SetActive(true);
        
        if (sliderFillImage == null && discomfortSlider != null)
            sliderFillImage = discomfortSlider.fillRect.GetComponent<Image>();
        
        if (discomfortSlider != null)
        {
            discomfortSlider.value = 0f;
            if (sliderFillImage != null)
                sliderFillImage.color = normalColor;
        }
    }

    void Update()
    {
        if (!problemSolved && !endingActivated)
        {
            globalDiscomfortTimer += Time.deltaTime;
            UpdateZone();
            UpdateDiscomfortGauge();
        }

        CheckEndingZone();
    }

    private void UpdateZone()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, peopleLayer);
        HashSet<Animator> currentAnimators = new HashSet<Animator>();

        // 1단계: 현재 범위 안 NPC 수집
        foreach (var hit in hits)
        {
            Animator anim = hit.GetComponentInParent<Animator>();
            if (anim == null) continue;
            currentAnimators.Add(anim);
        }

        // 2단계: 새로 들어온 NPC - 이모티콘 타이머만 등록
        foreach (var anim in currentAnimators)
        {
            if (!insideAnimators.Contains(anim))
            {
                animatorAngryEmojiTimers[anim] = 0f;
                if (!spawnedEmojis.ContainsKey(anim))
                    spawnedEmojis[anim] = new List<GameObject>();
                else
                    spawnedEmojis[anim].Clear();
            }
        }

        // 3단계: 나간 NPC - 이모티콘만 삭제
        List<Animator> toRemove = new List<Animator>();
        foreach (var anim in insideAnimators)
        {
            if (!currentAnimators.Contains(anim))
            {
                RemoveEmojisFor(anim);
                animatorAngryEmojiTimers.Remove(anim);
                spawnedEmojis.Remove(anim);
                toRemove.Add(anim);
            }
        }

        foreach (var anim in toRemove)
            insideAnimators.Remove(anim);

        // 4단계: 범위 안 NPC들 - 이모티콘 계속 생성
        foreach (var anim in currentAnimators)
        {
            if (!insideAnimators.Contains(anim))
                insideAnimators.Add(anim);

            if (animatorAngryEmojiTimers.ContainsKey(anim))
            {
                animatorAngryEmojiTimers[anim] += Time.deltaTime;

                if (animatorAngryEmojiTimers[anim] >= angryEmojiSpawnDelay)
                {
                    SpawnEmoji(anim);
                    animatorAngryEmojiTimers[anim] = 0f;
                }
            }
        }
    }

    private void SpawnEmoji(Animator anim)
    {
        if (angryEmojiPrefab == null) return;
        
        Vector3 spawnPos = anim.transform.position + Vector3.up * 11.0f;
        GameObject emoji = Instantiate(angryEmojiPrefab, spawnPos, Quaternion.Euler(90, 0, 0));
        
        // ★★★ [수정] 이모티콘을 캐릭터의 자식으로 설정하여 따라다니게 함 ★★★
        emoji.transform.SetParent(anim.transform);

        if (!spawnedEmojis.ContainsKey(anim))
            spawnedEmojis[anim] = new List<GameObject>();
        
        spawnedEmojis[anim].Add(emoji);
    }

    private void RemoveEmojisFor(Animator anim)
    {
        if (spawnedEmojis.ContainsKey(anim))
        {
            foreach (var emoji in spawnedEmojis[anim])
            {
                if (emoji != null) Destroy(emoji);
            }
            spawnedEmojis[anim].Clear();
        }
    }

    private void UpdateDiscomfortGauge()
    {
        float ratio = Mathf.Clamp01(globalDiscomfortTimer / maxDiscomfortDuration);

        if (discomfortSlider != null)
        {
            discomfortSlider.value = ratio;

            if (sliderFillImage != null)
            {
                if (ratio >= 0.5f)
                    sliderFillImage.color = warningColor;
                else
                    sliderFillImage.color = normalColor;
            }
        }
    }

    // ★ 엔딩 구역 체크
    private void CheckEndingZone()
    {
        if (endingActivated)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, endRadius, endingLayer);

        if (hits.Length > 0)
        {
            endingActivated = true;

            // 기존 정리 로직 재사용 (게이지/이모티콘 정리 & 비활성화)
            SolveProblem();

            // 엔딩 애니메이션/상태 전파
            TriggerEndingEventToPeople();
        }
    }

    // ★ 엔딩 발동 시 주변 사람들에게 트리거 날리기
    private void TriggerEndingEventToPeople()
    {
        HashSet<Animator> processed = new HashSet<Animator>();

        // 1) peopleLayer: EventEnd1 + 잠깐 멈췄다가 EventOff1 후 재개
        Collider[] people = Physics.OverlapSphere(transform.position, radius * 1.3f, peopleLayer);
        foreach (var p in people)
        {
            Animator anim = p.GetComponentInParent<Animator>();
            if (anim == null) continue;
            if (processed.Contains(anim)) continue;
            processed.Add(anim);

            if (!string.IsNullOrEmpty(endTriggerName))
            {
                anim.SetTrigger(endTriggerName);
            }

            WaypointWalker walker = anim.GetComponentInParent<WaypointWalker>();
            StartCoroutine(HandleEndAfterDelay(anim, walker, resumeWalkDelay));
        }

        // 2) extraEndLayer (peopleLayer2 역할): 동일하게 멈추고 박수(EventOff1) 후 다시 걷기
        if (extraEndLayer != 0)
        {
            Collider[] extra = Physics.OverlapSphere(transform.position, radius * 1.3f, extraEndLayer);
            foreach (var c in extra)
            {
                Animator anim = c.GetComponentInParent<Animator>();
                if (anim == null) continue;
                if (processed.Contains(anim)) continue;
                processed.Add(anim);

                if (!string.IsNullOrEmpty(endTriggerName))
                {
                    anim.SetTrigger(endTriggerName);   // EventEnd1
                }

                WaypointWalker walker = anim.GetComponentInParent<WaypointWalker>();
                StartCoroutine(HandleEndAfterDelay(anim, walker, resumeWalkDelay));
            }
        }

        // 3) EndCount는 씬의 모든 Animator에 대해 1씩, 단 한 번만 증가
        if (!endCountGiven && !string.IsNullOrEmpty(endCountParamName))
        {
            Animator[] allAnimators = FindObjectsOfType<Animator>();
            foreach (var anim in allAnimators)
            {
                int current = anim.GetInteger(endCountParamName);
                anim.SetInteger(endCountParamName, current + 1);
            }

            endCountGiven = true;  // 이 event_car 존에서는 다시 증가시키지 않음
        }
    }

    // ★ 엔딩 후 처리: 잠깐 멈췄다가 다시 걷고, EventOff 쏘기
    private IEnumerator HandleEndAfterDelay(Animator anim, WaypointWalker walker, float delay)
    {
        // 엔딩 시점에서 일단 멈추기 (walker가 있을 경우)
        if (walker != null)
        {
            walker.PauseWalking();
        }

        // 지정 시간 대기
        yield return new WaitForSeconds(delay);

        // 다시 걷기 시작
        if (walker != null)
        {
            walker.ResumeWalking();
        }

        // 애니메이션 해제 트리거 (EventOff 계열)
        if (anim != null && !string.IsNullOrEmpty(endOffTriggerName))
        {
            anim.SetTrigger(endOffTriggerName);
        }
    }

    public void SolveProblem()
    {
        problemSolved = true;
        
        foreach (var key in new List<Animator>(insideAnimators))
            RemoveEmojisFor(key);
        
        spawnedEmojis.Clear();
        animatorAngryEmojiTimers.Clear();
        insideAnimators.Clear();
        
        if (discomfortGaugeObject != null) 
            discomfortGaugeObject.SetActive(false);
    }
    
    private void OnDisable()
    {
        foreach (var key in new List<Animator>(insideAnimators))
            RemoveEmojisFor(key);
        
        spawnedEmojis.Clear();
    }

    // ============================================
    // ISensorReactiveEvent 구현부
    // 센서 A/B에서 호출되는 외부 진입점
    // ============================================
    // 센서 A: car 앞 물체가 사라질 때
    public void OnSensorHide()
    {
        // 1) 현재 상황을 "문제 해결"로 정리 (게이지 + 이모티콘 정리)
        SolveProblem();

        // 2) 엔딩 플래그 초기화
        endingActivated = false;

        // 3) 이 존 로직 자체를 끔 (센서 B에서 다시 켜줄 것)
        this.enabled = false;
    }

    // 센서 B: 다른 위치에서 car 관련 물체가 새로 감지될 때
    public void OnSensorShow()
    {
        // 1) 상태 초기화
        problemSolved = false;
        endingActivated = false;
        globalDiscomfortTimer = 0f;

        // 2) 게이지 다시 켜기 + 초기화
        if (discomfortGaugeObject != null)
            discomfortGaugeObject.SetActive(true);

        if (discomfortSlider != null)
        {
            discomfortSlider.value = 0f;
            if (sliderFillImage != null)
                sliderFillImage.color = normalColor;
        }

        // 3) 다시 로직 켜기
        this.enabled = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, endRadius);
    }
}