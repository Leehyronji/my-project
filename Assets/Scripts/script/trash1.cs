using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class trash1 : MonoBehaviour, ISensorReactiveEvent
{
    [Header("사람 탐색 설정")]
    public float radius = 7.2f;
    public LayerMask peopleLayer;
    public string animationTriggerName = "EventReact0";
    public string clearTriggerName = "EventOff0";

    [Header("★ 선택 가능 레이어 설정")]
    [Tooltip("머리 위에 이모티콘만 띄우고 싶은 선택 가능 대상 레이어")]
    public LayerMask selectableLayer;

    [Header("엔딩 구역 설정")]
    public float endRadius = 3f;
    public LayerMask endingLayer;
    public string endTriggerName = "EventEnd0";

    [Header("엔딩 후 재개 설정")]
    public float resumeWalkDelay = 7f;        // ★ 엔딩 후 다시 걷기까지 대기 시간(초)
    public string endOffTriggerName = "EventOff0"; // ★ 엔딩 해제 트리거

    [Header("★ 엔딩 카운트 설정")]
    [Tooltip("Animator 파라미터 이름 (예: EndCount)")]
    public string endCountParamName = "EndCount";

    [Header("★ 이모티콘 설정")]
    public GameObject angryEmojiPrefab;
    public float angryEmojiSpawnDelay = 3f;
    public float emojiLifetime = 2f;  // 이모티콘 생존 시간

    [Header("★ 불편함 게이지 설정")]
    public GameObject discomfortGaugeObject;  // 게이지 Canvas
    public Slider discomfortSlider;           // Slider 컴포넌트
    public Image sliderFillImage;             // Fill 이미지
    public Color normalColor = Color.green;   // 정상 색상
    public Color warningColor = Color.red;    // 경고 색상
    public float maxDiscomfortDuration = 30f; // 최대 불편함 시간

    [Header("★ 차 경적 활성화 설정")]
    [Tooltip("EndEvent0 발생 후, 차들이 경적을 울릴 수 있게 만들기까지의 지연 시간(초)")]
    public float carHornEnableDelay = 10f;

    private HashSet<Animator> insideAnimators = new HashSet<Animator>();
    private Dictionary<Animator, float> animatorAngryEmojiTimers = new Dictionary<Animator, float>();
    private Dictionary<Animator, float> animatorDiscomfortTimers = new Dictionary<Animator, float>();  // ★ 게이지 타이머
    private Dictionary<Animator, List<GameObject>> spawnedEmojis = new Dictionary<Animator, List<GameObject>>();
    private Dictionary<Animator, float> frozenYPositions = new Dictionary<Animator, float>();
    private Dictionary<Animator, bool> originalKinematicStates = new Dictionary<Animator, bool>();

    // ★ 선택 가능 레이어용 이모티콘 타이머
    private Dictionary<Animator, float> selectableEmojiTimers = new Dictionary<Animator, float>();
    
    private bool endingActivated = false;

    // ★ 이 trash1 존이 EndCount를 이미 한 번 올렸는지 여부
    private bool endCountGiven = false;

    void Start()
    {
        // ★ 게이지 초기화
        if (discomfortGaugeObject != null)
            discomfortGaugeObject.SetActive(false);
        
        if (sliderFillImage == null && discomfortSlider != null && discomfortSlider.fillRect != null)
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
        if (!endingActivated)
        {
            UpdateZone();                   // peopleLayer + 게이지용
            UpdateDiscomfortGauge();        // 불편함 게이지
            UpdateSelectableLayerEmojis();  // selectableLayer 이모티콘 전용
        }
        
        CheckEndingZone();
    }

    // ============================================
    // peopleLayer: 존 안에 들어오는 사람들
    // ============================================
    private void UpdateZone()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, peopleLayer);
        HashSet<Animator> currentAnimators = new HashSet<Animator>();

        foreach (var hit in hits)
        {
            Animator anim = hit.GetComponentInParent<Animator>();
            if (anim == null) continue;

            if (!currentAnimators.Add(anim))
                continue;

            if (!insideAnimators.Contains(anim))
            {
                // 처음 들어온 사람
                insideAnimators.Add(anim);

                // Y축 고정용 저장
                frozenYPositions[anim] = anim.transform.position.y;
                
                // 리액션 트리거
                if (!string.IsNullOrEmpty(animationTriggerName))
                    anim.SetTrigger(animationTriggerName);

                // 걷기/네비 정지
                WaypointWalker walker = anim.GetComponentInParent<WaypointWalker>();
                if (walker != null)
                {
                    walker.PauseWalking();
                    walker.enabled = false;
                }

                NavMeshAgent agent = anim.GetComponentInParent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                }

                Rigidbody rb = anim.GetComponentInParent<Rigidbody>();
                if (rb != null)
                {
                    originalKinematicStates[anim] = rb.isKinematic;
                    rb.isKinematic = true;
#if UNITY_6000_0_OR_NEWER
                    rb.linearVelocity = Vector3.zero;
#else
                    rb.velocity = Vector3.zero;
#endif
                }

                // ★ 타이머/이모티콘 리스트 초기화
                animatorAngryEmojiTimers[anim] = 0f;
                animatorDiscomfortTimers[anim] = 0f;
                spawnedEmojis[anim] = new List<GameObject>();
            }
        }

        // 존을 벗어난 사람 처리
        List<Animator> toRemove = new List<Animator>();
        foreach (var anim in insideAnimators)
        {
            if (!currentAnimators.Contains(anim))
            {
                if (anim != null)
                {
                    // ★★★ 범위를 벗어난 사람에게 EventOff0(= clearTriggerName) 트리거 쏘기
                    if (!string.IsNullOrEmpty(clearTriggerName))
                        anim.SetTrigger(clearTriggerName);

                    // NavMeshAgent 복구
                    NavMeshAgent agent = anim.GetComponentInParent<NavMeshAgent>();
                    if (agent != null && agent.isOnNavMesh)
                    {
                        NavMeshHit hit;
                        Vector3 currentPos = anim.transform.position;
                        
                        if (NavMesh.SamplePosition(currentPos, out hit, 2f, NavMesh.AllAreas))
                            agent.Warp(hit.position);
                        
                        agent.updatePosition = true;
                        agent.updateRotation = true;
                        agent.isStopped = false;
                        agent.velocity = Vector3.zero;
                    }

                    // Rigidbody 복구
                    Rigidbody rb = anim.GetComponentInParent<Rigidbody>();
                    if (rb != null && originalKinematicStates.ContainsKey(anim))
                    {
                        rb.isKinematic = originalKinematicStates[anim];
                        originalKinematicStates.Remove(anim);
                    }

                    // 걷기 재개
                    StartCoroutine(ResumeWalkingDelayed(anim, 0.2f));

                    // 내부 상태 제거
                    frozenYPositions.Remove(anim);
                    RemoveEmojisFor(anim);
                    animatorAngryEmojiTimers.Remove(anim);
                    animatorDiscomfortTimers.Remove(anim);
                    spawnedEmojis.Remove(anim);
                }
                toRemove.Add(anim);
            }
        }

        foreach (var anim in toRemove)
            insideAnimators.Remove(anim);

        // 존 안에 있는 사람들: Y 고정 + 이모티콘 타이머 증가
        foreach (var anim in currentAnimators)
        {
            // Y축 고정
            if (frozenYPositions.ContainsKey(anim))
            {
                Vector3 currentPos = anim.transform.position;
                anim.transform.position = new Vector3(currentPos.x, frozenYPositions[anim], currentPos.z);
            }

            // 타이머 증가 및 이모티콘 생성
            if (animatorAngryEmojiTimers.ContainsKey(anim))
            {
                animatorAngryEmojiTimers[anim] += Time.deltaTime;
                animatorDiscomfortTimers[anim] += Time.deltaTime;

                if (animatorAngryEmojiTimers[anim] >= angryEmojiSpawnDelay)
                {
                    SpawnEmoji(anim);
                    animatorAngryEmojiTimers[anim] = 0f;
                }
            }
        }
    }

    // ============================================
    // selectableLayer: 이모티콘만 뜨는 대상
    // ============================================
    private void UpdateSelectableLayerEmojis()
    {
        if (selectableLayer == 0) return; // 레이어 미설정 시 무시

        Collider[] hits = Physics.OverlapSphere(transform.position, radius, selectableLayer);
        HashSet<Animator> currentSelectables = new HashSet<Animator>();

        foreach (var hit in hits)
        {
            Animator anim = hit.GetComponentInParent<Animator>();
            if (anim == null) continue;

            // 이미 trash 안(peopleLayer)에서 멈춰 있는 애는 여기서 또 처리 안 함
            if (insideAnimators.Contains(anim))
                continue;

            currentSelectables.Add(anim);

            // 타이머/이모티콘 리스트 초기화
            if (!selectableEmojiTimers.ContainsKey(anim))
            {
                selectableEmojiTimers[anim] = 0f;

                if (!spawnedEmojis.ContainsKey(anim))
                    spawnedEmojis[anim] = new List<GameObject>();
            }
        }

        // 범위를 벗어난 선택 가능 대상 정리
        List<Animator> toRemove = new List<Animator>();
        foreach (var kv in selectableEmojiTimers)
        {
            Animator anim = kv.Key;
            if (!currentSelectables.Contains(anim))
            {
                // ★★★ selectableLayer도 범위 벗어나면 EventOff0 쏴주기
                if (anim != null && !string.IsNullOrEmpty(clearTriggerName))
                    anim.SetTrigger(clearTriggerName);

                RemoveEmojisFor(anim);
                toRemove.Add(anim);
            }
        }
        foreach (var anim in toRemove)
            selectableEmojiTimers.Remove(anim);

        // 범위 안에 있는 선택 가능 대상에 대해 타이머 증가 및 이모티콘 스폰
        foreach (var anim in currentSelectables)
        {
            selectableEmojiTimers[anim] += Time.deltaTime;

            if (selectableEmojiTimers[anim] >= angryEmojiSpawnDelay)
            {
                SpawnEmoji(anim);
                selectableEmojiTimers[anim] = 0f;
            }
        }
    }

    // ============================================
    // 게이지 로직 (peopleLayer용)
    // ============================================
    private void UpdateDiscomfortGauge()
    {
        // NPC가 한 명도 없으면 게이지 숨기기
        if (animatorDiscomfortTimers.Count == 0)
        {
            if (discomfortGaugeObject != null)
                discomfortGaugeObject.SetActive(false);
            
            if (discomfortSlider != null)
                discomfortSlider.value = 0f;
            
            return;
        }

        // 게이지 표시
        if (discomfortGaugeObject != null)
            discomfortGaugeObject.SetActive(true);

        // 가장 오래 머문 시간 찾기
        float maxTime = 0f;
        foreach (var time in animatorDiscomfortTimers.Values)
            maxTime = Mathf.Max(maxTime, time);

        // 게이지 비율 계산
        float ratio = Mathf.Clamp01(maxTime / maxDiscomfortDuration);

        // Slider 값 설정
        if (discomfortSlider != null)
        {
            discomfortSlider.value = ratio;

            if (sliderFillImage != null)
                sliderFillImage.color = (ratio >= 0.5f) ? warningColor : normalColor;
        }
    }

    private IEnumerator ResumeWalkingDelayed(Animator anim, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (anim != null)
        {
            WaypointWalker walker = anim.GetComponentInParent<WaypointWalker>();
            if (walker != null)
            {
                walker.enabled = true;
                walker.ResumeWalking();
            }
        }
    }

    // ============================================
    // 이모티콘 생성 / 제거
    // ============================================
    private void SpawnEmoji(Animator anim)
    {
        if (angryEmojiPrefab == null || anim == null) return;

        Vector3 spawnPos = anim.transform.position + Vector3.up * 11.0f;
        GameObject emoji = Instantiate(angryEmojiPrefab, spawnPos, Quaternion.Euler(90, 0, 0));

        // 캐릭터 자식으로 붙여서 따라다니게
        emoji.transform.SetParent(anim.transform);

        if (emojiLifetime > 0f)
            Destroy(emoji, emojiLifetime);

        if (!spawnedEmojis.ContainsKey(anim))
            spawnedEmojis[anim] = new List<GameObject>();

        spawnedEmojis[anim].Add(emoji);
    }

    private void RemoveEmojisFor(Animator anim)
    {
        if (!spawnedEmojis.ContainsKey(anim)) return;

        foreach (var emoji in spawnedEmojis[anim])
        {
            if (emoji != null) Destroy(emoji);
        }
        spawnedEmojis[anim].Clear();
    }

    // ============================================
    // 엔딩 존 진입 처리
    // ============================================
    private void CheckEndingZone()
    {
        if (endingActivated)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, endRadius, endingLayer);

        if (hits.Length > 0)
        {
            endingActivated = true;
            
            // 모든 정리
            foreach (var key in new List<Animator>(insideAnimators))
                RemoveEmojisFor(key);
            
            spawnedEmojis.Clear();
            animatorAngryEmojiTimers.Clear();
            animatorDiscomfortTimers.Clear();
            frozenYPositions.Clear();
            originalKinematicStates.Clear();
            insideAnimators.Clear();
            selectableEmojiTimers.Clear();
            
            // 게이지 숨기기
            if (discomfortGaugeObject != null)
                discomfortGaugeObject.SetActive(false);
            
            TriggerEndingEventToPeople();
        }
    }

    private void TriggerEndingEventToPeople()
    {
        HashSet<Animator> processed = new HashSet<Animator>();

        // 1) peopleLayer: EventEnd0 + 잠깐 멈췄다가 다시 걷기
        Collider[] people = Physics.OverlapSphere(transform.position, radius * 14f, peopleLayer);
        foreach (var p in people)
        {
            Animator anim = p.GetComponentInParent<Animator>();
            if (anim == null) continue;
            if (processed.Contains(anim)) continue;
            processed.Add(anim);

            if (!string.IsNullOrEmpty(endTriggerName))
            {
                anim.SetTrigger(endTriggerName);

                WaypointWalker walker = anim.GetComponentInParent<WaypointWalker>();
                StartCoroutine(HandleEndAfterDelay(anim, walker, resumeWalkDelay));
            }
        }

        // 2) selectableLayer: EventEnd0 + 잠깐 멈췄다가 다시 걷기
        if (selectableLayer != 0)
        {
            Collider[] selectableHits = Physics.OverlapSphere(transform.position, radius * 3f, selectableLayer);
            foreach (var c in selectableHits)
            {
                Animator anim = c.GetComponentInParent<Animator>();
                if (anim == null) continue;
                if (processed.Contains(anim)) continue;
                processed.Add(anim);

                if (!string.IsNullOrEmpty(endTriggerName))
                {
                    anim.SetTrigger(endTriggerName);

                    WaypointWalker walker = anim.GetComponentInParent<WaypointWalker>();
                    StartCoroutine(HandleEndAfterDelay(anim, walker, resumeWalkDelay));
                }
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

            endCountGiven = true;
        }

        // 4) ★ 일정 시간 후 모든 car_01의 경적 활성화
        StartCoroutine(EnableCarHornsAfterDelay());

        // 엔딩 처리 끝났으니 이 존 비활성화
        this.enabled = false;
    }

    private IEnumerator EnableCarHornsAfterDelay()
    {
        yield return new WaitForSeconds(carHornEnableDelay);

        car_01[] cars = FindObjectsOfType<car_01>();
        foreach (var car in cars)
        {
            car.SetHornEnabled(true);
        }

        Debug.Log($"[trash1] EndEvent0 이후 {carHornEnableDelay}초 경과 → car_01 {FindObjectsOfType<car_01>().Length}대 경적 활성화");
    }

    private IEnumerator HandleEndAfterDelay(Animator anim, WaypointWalker walker, float delay)
    {
        if (walker != null)
            walker.PauseWalking();

        yield return new WaitForSeconds(delay);

        if (anim != null)
        {
            NavMeshAgent agent = anim.GetComponentInParent<NavMeshAgent>();
            if (agent != null)
            {
                agent.updatePosition = true;
                agent.updateRotation = true;
                agent.isStopped = false;
                agent.velocity = Vector3.zero;
            }
        }

        if (walker != null)
        {
            walker.enabled = true;
            walker.ResumeWalking();
        }

        if (anim != null && !string.IsNullOrEmpty(endOffTriggerName))
            anim.SetTrigger(endOffTriggerName);
    }

    private void OnDisable()
    {
        foreach (var key in new List<Animator>(insideAnimators))
            RemoveEmojisFor(key);
        
        spawnedEmojis.Clear();
        frozenYPositions.Clear();
        originalKinematicStates.Clear();
        animatorDiscomfortTimers.Clear();
        animatorAngryEmojiTimers.Clear();
        selectableEmojiTimers.Clear();
        insideAnimators.Clear();
        
        if (discomfortGaugeObject != null)
            discomfortGaugeObject.SetActive(false);
    }

    // ============================================
    // ISensorReactiveEvent 구현부
    // ============================================
    public void ForceClearAll()
    {
        List<Animator> toClear = new List<Animator>(insideAnimators);

        foreach (var anim in toClear)
        {
            if (anim == null)
                continue;

            if (!string.IsNullOrEmpty(clearTriggerName))
                anim.SetTrigger(clearTriggerName);

            NavMeshAgent agent = anim.GetComponentInParent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
            {
                NavMeshHit hit;
                Vector3 currentPos = anim.transform.position;

                if (NavMesh.SamplePosition(currentPos, out hit, 2f, NavMesh.AllAreas))
                    agent.Warp(hit.position);

                agent.updatePosition = true;
                agent.updateRotation = true;
                agent.isStopped = false;
                agent.velocity = Vector3.zero;
            }

            Rigidbody rb = anim.GetComponentInParent<Rigidbody>();
            if (rb != null && originalKinematicStates.ContainsKey(anim))
            {
                rb.isKinematic = originalKinematicStates[anim];
                originalKinematicStates.Remove(anim);
            }

            StartCoroutine(ResumeWalkingDelayed(anim, 0.2f));

            frozenYPositions.Remove(anim);
            RemoveEmojisFor(anim);
            animatorAngryEmojiTimers.Remove(anim);
            animatorDiscomfortTimers.Remove(anim);
            spawnedEmojis.Remove(anim);
            insideAnimators.Remove(anim);
        }

        selectableEmojiTimers.Clear();

        if (animatorDiscomfortTimers.Count == 0)
        {
            if (discomfortGaugeObject != null)
                discomfortGaugeObject.SetActive(false);

            if (discomfortSlider != null)
                discomfortSlider.value = 0f;
        }
    }

    public void OnSensorHide()
    {
        ForceClearAll();
        endingActivated = false;
        this.enabled = false;
    }

    public void OnSensorShow()
    {
        endingActivated = false;

        if (discomfortGaugeObject != null)
            discomfortGaugeObject.SetActive(false);

        if (discomfortSlider != null)
        {
            discomfortSlider.value = 0f;
            if (sliderFillImage != null)
                sliderFillImage.color = normalColor;
        }

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