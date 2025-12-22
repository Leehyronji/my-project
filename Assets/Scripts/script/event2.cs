using System.Collections;                 // ★ 코루틴용 추가
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class event2 : MonoBehaviour
{
    [Header("사람 탐색 설정")]
    public float radius = 15f;
    public LayerMask peopleLayer;

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
    public string endOffTriggerName = "EventOff1";   // ★ 엔딩 해제 트리거

    [Header("엔딩 후 재개 설정")]
    public float resumeWalkDelay = 7f;               // ★ 엔딩 후 다시 걷기 + EventOff까지 대기 시간(초)

    private float globalDiscomfortTimer = 0f;
    private HashSet<Animator> insideAnimators = new HashSet<Animator>();
    private Dictionary<Animator, float> animatorAngryEmojiTimers = new Dictionary<Animator, float>();
    private Dictionary<Animator, List<GameObject>> spawnedEmojis = new Dictionary<Animator, List<GameObject>>();
    
    private bool problemSolved = false;
    private bool endingActivated = false;

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
                // ★ 애니메이션/이동은 건드리지 않고, 이모티콘만 관리
                animatorAngryEmojiTimers[anim] = 0f;
                spawnedEmojis[anim] = new List<GameObject>();
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
        
        Vector3 spawnPos = anim.transform.position + Vector3.up * 2.5f;
        GameObject emoji = Instantiate(angryEmojiPrefab, spawnPos, Quaternion.Euler(90, 0, 0));
        
        if (spawnedEmojis.ContainsKey(anim))
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

    // ★ 엔딩 구역 체크 (event_car와 동일 패턴)
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
        // event_car와 마찬가지로 radius를 약간 확장해서 사용
        Collider[] people = Physics.OverlapSphere(transform.position, radius * 4f / 3f, peopleLayer);
        foreach (var p in people)
        {
            Animator anim = p.GetComponentInParent<Animator>();
            if (anim == null) continue;

            // 1) 엔딩 트리거 먼저
            if (!string.IsNullOrEmpty(endTriggerName))
            {
                anim.SetTrigger(endTriggerName);
            }

            // 2) WaypointWalker 있으면 멈추고, 일정 시간 후에 다시 걷게 + EventOff 날리기
            WaypointWalker walker = anim.GetComponentInParent<WaypointWalker>();
            StartCoroutine(HandleEndAfterDelay(anim, walker, resumeWalkDelay));
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
        
        foreach(var key in new List<Animator>(insideAnimators))
            RemoveEmojisFor(key);
        
        spawnedEmojis.Clear();
        animatorAngryEmojiTimers.Clear();
        insideAnimators.Clear();
        
        if (discomfortGaugeObject != null) 
            discomfortGaugeObject.SetActive(false);
    }
    
    private void OnDisable()
    {
        foreach(var key in new List<Animator>(insideAnimators))
            RemoveEmojisFor(key);
        
        spawnedEmojis.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, endRadius);
    }
}