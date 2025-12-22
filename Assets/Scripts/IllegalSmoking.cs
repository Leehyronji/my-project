using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

public class IllegalSmoking : MonoBehaviour, ISensorReactiveEvent
{
    [Header("Detection Settings")]
    public float radius = 5f;
    public LayerMask peopleLayer;

    [Header("Smoking Timing")]
    public float minDelay = 0.0f;
    public float maxDelay = 2.0f;

    [Header("Animation Trigger Names")]
    public string smokingTrigger = "SmokingOn";
    public string smokingOffTrigger = "SmokingOff";

    [Header("★ 3D 게이지 설정")]
    public GameObject discomfortGaugeObject;
    public Slider discomfortSlider;
    public Image sliderFillImage;
    public Color normalColor = Color.green;
    public Color warningColor = Color.red;
    public float maxDiscomfortDuration = 30f;

    [Header("★ 화난 이모티콘 설정")]
    public GameObject angryEmojiPrefab;
    public float angryEmojiSpawnDelay = 3f;

    [Tooltip("이모티콘이 자동으로 사라지는 시간(초). 0 이하이면 자동 삭제 없음")]
    public float angryEmojiLifetime = 5f;

    [Header("★ 화난 이모티콘 외곽 감지 설정")]
    [Tooltip("기본 radius에 곱해지는 배수 (예: 2면 radius*2 범위까지 감지)")]
    public float angryOuterRadiusMultiplier = 2f;

    [Tooltip("외곽 범위 안에 있을 때 이모티콘 스폰 주기(초)")]
    public float outerAngrySpawnDelay = 3f;

    [Tooltip("외곽 화난 이모티콘을 적용할 사람들의 레이어 (흡연자와는 분리된 전용 레이어)")]
    public LayerMask outerAngryLayer;

    [Header("엔딩 구역 설정")]
    public float endRadius = 3f;
    public LayerMask endingLayer;
    public string endTriggerName = "EventEnd2";
    public string endOffTriggerName = "EventOff2";

    [Header("센서 연동용 Ending2 포인트")]
    public Transform endingPoint;  // ← ending2 GameObject의 Transform을 Inspector에서 연결

    [Header("엔딩 후 재개 설정")]
    public float resumeWalkDelay = 7f;   // 엔딩 연출 후 다시 걷기 + EventOff까지 대기 시간(초)

    [Header("Smoke Effect Settings")]
    public ParticleSystem smokeEffect;      // 연기 파티클
    public int smokeTriggerCount = 2;       // 이 인원 이상이면 연기

    [Header("Smoking Area Settings")]
    public LayerMask smokingAreaLayer;      // 합법 SmokingArea가 속한 레이어

    [Header("★ 엔딩 카운트 설정")]
    [Tooltip("Animator의 EndCount 파라미터 이름")]
    public string endCountParamName = "EndCount";

    private HashSet<Animator> insideAnimators = new HashSet<Animator>();
    private Dictionary<Animator, Coroutine> pendingCoroutines = new Dictionary<Animator, Coroutine>();
    private Dictionary<Animator, float> animatorDiscomfortTimers = new Dictionary<Animator, float>();
    private Dictionary<Animator, float> animatorAngryEmojiTimers = new Dictionary<Animator, float>();
    private Dictionary<Animator, List<GameObject>> spawnedEmojis = new Dictionary<Animator, List<GameObject>>();
    
    // ★ Y축 고정을 위한 딕셔너리
    private Dictionary<Animator, float> frozenYPositions = new Dictionary<Animator, float>();

    // ★ 외곽 감지 이모티콘용 컬렉션
    private HashSet<Animator> outerAngryAnimators = new HashSet<Animator>();
    private Dictionary<Animator, float> outerAngryTimers = new Dictionary<Animator, float>();

    // 연기 파티클 상태
    private bool isSmokePlaying = false;

    private bool endingActivated = false;

    // ★ 이 존이 EndCount를 이미 한 번 올렸는지 여부
    private bool endCountGiven = false;

    void Start()
    {
        if (discomfortGaugeObject != null)
            discomfortGaugeObject.SetActive(false);

        if (sliderFillImage == null && discomfortSlider != null && discomfortSlider.fillRect != null)
        {
            sliderFillImage = discomfortSlider.fillRect.GetComponent<Image>();
        }
    }

    void Update()
    {
        if (!endingActivated)
        {
            // 0) 합법 SmokingArea가 반경 안에 있는지 먼저 체크
            if (IsSmokingAreaPresent())
            {
                List<Animator> smokersAtEnd = new List<Animator>(insideAnimators);

                // 합법 구역이 감지되면: 전원 SmokingOff + 이동 재개 + 연기 종료 + 게이지 리셋
                ForceStopAllSmoking();
                ClearOuterAngryState();
                UpdateDiscomfortGauge();
                TriggerEndingEventToPeople(smokersAtEnd);
                return; // 이 프레임은 더 이상 처리하지 않음
            }

            // 1) 사람 감지 (흡연 반경)
            Collider[] hits = Physics.OverlapSphere(transform.position, radius, peopleLayer);
            HashSet<Animator> currentFrame = new HashSet<Animator>();

            foreach (var hit in hits)
            {
                Animator anim = hit.GetComponentInParent<Animator>();
                if (anim == null) continue;

                currentFrame.Add(anim);

                if (insideAnimators.Contains(anim)) continue;
                if (pendingCoroutines.ContainsKey(anim)) continue;

                float delay = Random.Range(minDelay, maxDelay);
                Coroutine co = StartCoroutine(DelayedSmokingOn(anim, delay));
                pendingCoroutines.Add(anim, co);
            }

            // 2) 반경 밖으로 나간 사람 정리
            List<Animator> toRemoveInside = new List<Animator>();

            foreach (var anim in insideAnimators)
            {
                if (!currentFrame.Contains(anim))
                {
                    anim.SetTrigger(smokingOffTrigger);

                    WaypointWalker walker = anim.GetComponentInParent<WaypointWalker>();
                    if (walker != null) 
                    {
                        walker.enabled = true;
                        walker.ResumeWalking();
                    }

                    NavMeshAgent agent = anim.GetComponentInParent<NavMeshAgent>();
                    if (agent != null)
                    {
                        agent.updatePosition = true;
                        agent.updateRotation = true;
                        agent.isStopped = false;
                    }

                    // Y축 고정 해제
                    frozenYPositions.Remove(anim);

                    RemoveEmojisFor(anim);
                    animatorDiscomfortTimers.Remove(anim);
                    animatorAngryEmojiTimers.Remove(anim);
                    spawnedEmojis.Remove(anim);

                    toRemoveInside.Add(anim);
                }
            }

            foreach (var anim in toRemoveInside)
                insideAnimators.Remove(anim);

            // 3) 반경에서 나간 사람의 pending coroutine 취소
            List<Animator> toRemovePending = new List<Animator>();
            foreach (var kv in pendingCoroutines)
            {
                Animator anim = kv.Key;
                if (!currentFrame.Contains(anim))
                {
                    StopCoroutine(kv.Value);
                    toRemovePending.Add(anim);
                }
            }
            foreach (var anim in toRemovePending)
                pendingCoroutines.Remove(anim);

            // 4) 흡연 중인 NPC들의 Y축 고정 + 게이지/이모티콘
            foreach (var anim in insideAnimators)
            {
                // Y축 고정
                if (frozenYPositions.ContainsKey(anim))
                {
                    Vector3 currentPos = anim.transform.position;
                    anim.transform.position = new Vector3(currentPos.x, frozenYPositions[anim], currentPos.z);
                }

                if (!animatorDiscomfortTimers.ContainsKey(anim)) continue;

                animatorDiscomfortTimers[anim] += Time.deltaTime;
                animatorAngryEmojiTimers[anim] += Time.deltaTime;

                // ★ 흡연자 머리 위에는 이모티콘 띄우지 않음 (원하시면 주석 해제)
                if (animatorAngryEmojiTimers[anim] >= angryEmojiSpawnDelay)
                {
                    // SpawnEmoji(anim);  // 흡연자는 생략
                    animatorAngryEmojiTimers[anim] = 0f;
                }
            }

            // 4-1) ★ 외곽( radius * angryOuterRadiusMultiplier ) 내 사람들에 대한 화난 이모티콘 처리
            UpdateOuterAngryEmojis();

            // 5) 게이지, 연기 파티클 업데이트
            UpdateDiscomfortGauge();
            UpdateSmokeEffect();
        }

        CheckEndingZone();
    }

    private IEnumerator DelayedSmokingOn(Animator anim, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!IsInRadius(anim))
        {
            pendingCoroutines.Remove(anim);
            yield break;
        }

        // Y축 위치 저장 (담배 피는 순간의 높이 고정)
        frozenYPositions[anim] = anim.transform.position.y;

        anim.SetTrigger(smokingTrigger);

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

        // Rigidbody도 Kinematic으로 (물리 영향 차단)
        Rigidbody rb = anim.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
        }

        insideAnimators.Add(anim);

        if (!animatorDiscomfortTimers.ContainsKey(anim))
        {
            animatorDiscomfortTimers[anim] = 0f;
            animatorAngryEmojiTimers[anim] = 0f;
            if (!spawnedEmojis.ContainsKey(anim))
                spawnedEmojis[anim] = new List<GameObject>();
        }

        pendingCoroutines.Remove(anim);
    }

    private bool IsInRadius(Animator anim)
    {
        Vector3 pos = anim.transform.position;
        return Vector3.Distance(pos, transform.position) <= radius;
    }

    private void SpawnEmoji(Animator anim)
    {
        if (angryEmojiPrefab == null) return;

        Vector3 spawnPos = anim.transform.position + Vector3.up *11.0f;
        GameObject emoji = Instantiate(angryEmojiPrefab, spawnPos, Quaternion.Euler(90, 0, 0));

        // ★★★ [수정] 이모티콘을 캐릭터의 자식으로 설정하여 따라다니게 함 ★★★
        emoji.transform.SetParent(anim.transform);

        // ★ 처음 보는 Animator도 안전하게 리스트를 만들어 줌
        if (!spawnedEmojis.ContainsKey(anim))
        {
            spawnedEmojis[anim] = new List<GameObject>();
        }
        spawnedEmojis[anim].Add(emoji);

        // ★ 설정된 시간 뒤에 자동으로 삭제
        if (angryEmojiLifetime > 0f)
        {
            StartCoroutine(DestroyEmojiAfter(anim, emoji, angryEmojiLifetime));
        }
    }

    private IEnumerator DestroyEmojiAfter(Animator anim, GameObject emoji, float lifetime)
    {
        yield return new WaitForSeconds(lifetime);

        // 리스트에서 제거
        if (spawnedEmojis.ContainsKey(anim))
        {
            spawnedEmojis[anim].Remove(emoji);
        }

        // 실제 오브젝트 삭제
        if (emoji != null)
        {
            Destroy(emoji);
        }
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

    private void UpdateDiscomfortGauge()
    {
        if (animatorDiscomfortTimers.Count == 0)
        {
            if (discomfortGaugeObject != null)
                discomfortGaugeObject.SetActive(false);

            if (discomfortSlider != null)
                discomfortSlider.value = 0f;

            return;
        }

        if (discomfortGaugeObject != null)
            discomfortGaugeObject.SetActive(true);

        float maxTime = 0f;
        foreach (var t in animatorDiscomfortTimers.Values)
            maxTime = Mathf.Max(maxTime, t);

        float ratio = Mathf.Clamp01(maxTime / maxDiscomfortDuration);

        if (discomfortSlider != null)
        {
            discomfortSlider.value = ratio;

            if (sliderFillImage != null)
            {
                sliderFillImage.color = (ratio >= 0.5f) ? warningColor : normalColor;
            }
        }
    }

    // =========================
    // ★ 외곽 화난 이모티콘 관련 로직
    // =========================

    // 흡연자가 한 명이라도 있는지 체크
    private bool IsAnyoneSmoking()
    {
        return insideAnimators.Count > 0;
    }

    private void UpdateOuterAngryEmojis()
    {
        if (angryOuterRadiusMultiplier <= 1f)
        {
            // 1 이하이면 radius와 동일하거나 더 작으니, 굳이 별도 외곽 로직을 돌리지 않음
            return;
        }

        // 흡연자가 한 명도 없으면 외곽 이모티콘은 전부 제거
        if (!IsAnyoneSmoking())
        {
            ClearOuterAngryState();
            return;
        }

        float outerRadius = radius * angryOuterRadiusMultiplier;

        // 외곽 전용 레이어에 대해서만 감지
        Collider[] hits = Physics.OverlapSphere(transform.position, outerRadius, outerAngryLayer);

        HashSet<Animator> currentSet = new HashSet<Animator>();

        foreach (var hit in hits)
        {
            Animator anim = hit.GetComponentInParent<Animator>();
            if (anim == null) continue;

            // 흡연자 본인은 외곽 이모티콘 대상에서 제외
            if (insideAnimators.Contains(anim)) continue;

            currentSet.Add(anim);

            if (!outerAngryAnimators.Contains(anim))
            {
                outerAngryAnimators.Add(anim);
                outerAngryTimers[anim] = 0f;

                // spawnedEmojis 쪽도 안전하게 초기화
                if (!spawnedEmojis.ContainsKey(anim))
                    spawnedEmojis[anim] = new List<GameObject>();
            }
        }

        // 범위를 벗어난 Animator 정리
        List<Animator> toRemove = new List<Animator>();
        foreach (var anim in outerAngryAnimators)
        {
            if (!currentSet.Contains(anim))
            {
                RemoveEmojisFor(anim);
                toRemove.Add(anim);
                outerAngryTimers.Remove(anim);
            }
        }
        foreach (var anim in toRemove)
        {
            outerAngryAnimators.Remove(anim);
        }

        // 범위 안에 있는 Animator들에 대해 타이머 증가 & 일정 주기마다 이모티콘 스폰
        foreach (var anim in outerAngryAnimators)
        {
            if (!outerAngryTimers.ContainsKey(anim))
                outerAngryTimers[anim] = 0f;

            outerAngryTimers[anim] += Time.deltaTime;

            if (outerAngryTimers[anim] >= outerAngrySpawnDelay)
            {
                SpawnEmoji(anim);
                outerAngryTimers[anim] = 0f;
            }
        }
    }

    private void ClearOuterAngryState()
    {
        foreach (var anim in outerAngryAnimators)
        {
            RemoveEmojisFor(anim);
        }
        outerAngryAnimators.Clear();
        outerAngryTimers.Clear();
    }

    // =========================
    // Smoke Effect 관련
    // =========================

    // 연기 파티클 On/Off 제어
    private void UpdateSmokeEffect()
    {
        if (smokeEffect == null)
            return;

        int smokingCount = insideAnimators.Count;

        // 기준 이상 → 연기 켜기
        if (smokingCount >= smokeTriggerCount)
        {
            if (!isSmokePlaying)
            {
                smokeEffect.Play();
                isSmokePlaying = true;
            }
        }
        else
        {
            // 기준 미만이면 연기 끄기
            if (isSmokePlaying)
            {
                smokeEffect.Stop();
                isSmokePlaying = false;
            }
        }
    }

    // SmokingArea(합법 구역) 감지
    private bool IsSmokingAreaPresent()
    {
        // 같은 radius 기준으로 SmokingArea 레이어를 쿼리
        Collider[] areas = Physics.OverlapSphere(transform.position, radius, smokingAreaLayer);
        return areas.Length > 0;
    }

    // 합법 구역 등장 시: 모든 사람에게 SmokingOff + 이동 재개 + 연기 종료 + 게이지 리셋
    private void ForceStopAllSmoking()
    {
        // 1) 이미 SmokingOn 된 사람들 정리
        foreach (var anim in insideAnimators)
        {
            if (anim == null) continue;

            anim.SetTrigger(smokingOffTrigger);

            WaypointWalker walker = anim.GetComponentInParent<WaypointWalker>();
            if (walker != null)
            {
                walker.enabled = true;
                walker.ResumeWalking();
            }

            NavMeshAgent agent = anim.GetComponentInParent<NavMeshAgent>();
            if (agent != null)
            {
                agent.updatePosition = true;
                agent.updateRotation = true;
                agent.isStopped = false;
            }

            frozenYPositions.Remove(anim);
            RemoveEmojisFor(anim);
        }

        insideAnimators.Clear();

        // 2) 대기 중인 코루틴도 전부 취소
        foreach (var kv in pendingCoroutines)
        {
            if (kv.Value != null)
                StopCoroutine(kv.Value);
        }
        pendingCoroutines.Clear();

        // 3) 게이지 타이머/이모티콘 타이머 초기화
        animatorDiscomfortTimers.Clear();
        animatorAngryEmojiTimers.Clear();
        spawnedEmojis.Clear();

        // 4) 게이지 UI 리셋
        if (discomfortGaugeObject != null)
            discomfortGaugeObject.SetActive(false);
        if (discomfortSlider != null)
            discomfortSlider.value = 0f;

        // 5) 연기 이펙트 종료
        if (smokeEffect != null && isSmokePlaying)
        {
            smokeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        isSmokePlaying = false;
    }

    // =========================
    // 엔딩 관련 (기존 + 연동)
    // =========================

    // 엔딩 구역 체크
    private void CheckEndingZone()
    {
        if (endingActivated)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, endRadius, endingLayer);

        if (hits.Length > 0)
        {
            endingActivated = true; 

            // ★ 엔딩 시점에 흡연 중이던 사람들 스냅샷
            List<Animator> smokersAtEnd = new List<Animator>(insideAnimators);

            // 현재 흡연/이모티콘/게이지/연기 상태 정리
            ForceStopAllSmoking();
            ClearOuterAngryState();

            // 엔딩 애니메이션/상태 전파 + 흡연자 포함 즉시 걷기 정지
            TriggerEndingEventToPeople(smokersAtEnd);
        }
    }

    // 엔딩 발동 시 주변 사람들에게 트리거 날리기 + 흡연자 포함 걷기 정지
    private void TriggerEndingEventToPeople(List<Animator> smokersAtEnd)
    {
        HashSet<Animator> processed = new HashSet<Animator>();

        float range = radius * 1.3f;  // peopleLayer용 기본 엔딩 범위

        // 1) peopleLayer 대상: EventEnd2 + 걷기 멈춤 → delay 후 재개
        Collider[] people = Physics.OverlapSphere(transform.position, range, peopleLayer);
        foreach (var p in people)
        {
            Animator anim = p.GetComponentInParent<Animator>();
            if (anim == null) continue;
            if (processed.Contains(anim)) continue;
            processed.Add(anim);

            // SmokingOff + EventEnd2
            anim.SetTrigger(smokingOffTrigger);
            anim.SetTrigger(endTriggerName);

            WaypointWalker walker = anim.GetComponentInParent<WaypointWalker>();
            if (walker != null) walker.PauseWalking();

            NavMeshAgent agent = anim.GetComponentInParent<NavMeshAgent>();
            if (agent != null)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            StartCoroutine(HandleEndAfterDelay(anim, walker, resumeWalkDelay));
        }

        // 2) Outer Angry Layer 대상: EventEnd2만 보냄 (걷기 멈추지 않음)
        Collider[] outerHits = Physics.OverlapSphere(transform.position, radius * angryOuterRadiusMultiplier, outerAngryLayer);
        foreach (var hit in outerHits)
        {
            Animator anim = hit.GetComponentInParent<Animator>();
            if (anim == null) continue;

            WaypointWalker walker = anim.GetComponentInParent<WaypointWalker>();
            if (walker != null) walker.PauseWalking();

            NavMeshAgent agent = anim.GetComponentInParent<NavMeshAgent>();
            if (agent != null)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            // 흡연자(insideAnimators)는 이미 처리됨 → 제외
            if (insideAnimators.Contains(anim)) continue;

            // peopleLayer에서 이미 처리된 NPC는 제외
            if (processed.Contains(anim)) continue;

            processed.Add(anim);

            // ★ EventEnd2만 전달
            if (!string.IsNullOrEmpty(endTriggerName))
            {
                anim.SetTrigger(endTriggerName);
            }
        }

        // 3) 당시 흡연자들도 강제 정지
        if (smokersAtEnd != null)
        {
            foreach (var smoker in smokersAtEnd)
            {
                if (smoker == null) continue;

                WaypointWalker walker = smoker.GetComponentInParent<WaypointWalker>();
                if (walker != null) walker.PauseWalking();

                NavMeshAgent agent = smoker.GetComponentInParent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
            }
        }

        // 4) EndCount 증가 (존 당 1회만)
        if (!endCountGiven && !string.IsNullOrEmpty(endCountParamName))
        {
            Animator[] allAnimators = FindObjectsOfType<Animator>();
            foreach (var anim in allAnimators)
            {
                int current = anim.GetInteger(endCountParamName);
                anim.SetInteger(endCountParamName, current + 1);
            }

            endCountGiven = true;
            Debug.Log("[IllegalSmoking] EndCount +1 (모든 Animator)");
        }
    }

    // 엔딩 후 처리: 잠깐 멈췄다가 다시 걷고, EventOff 쏘기
    private IEnumerator HandleEndAfterDelay(Animator anim, WaypointWalker walker, float delay)
    {
        // 지정 시간 대기
        yield return new WaitForSeconds(delay);

        // 다시 걷기 시작
        if (walker != null)
        {
            walker.ResumeWalking();
        }

        // 엔딩 해제 트리거 (EventOff 계열)
        if (anim != null && !string.IsNullOrEmpty(endOffTriggerName))
        {
            anim.SetTrigger(smokingOffTrigger);
            anim.SetTrigger(endOffTriggerName);
        }
    }

    // =========================
    // ISensorReactiveEvent 구현부
    // =========================

    // 센서 A: "문제가 해소되었다" / "이 구역 이벤트를 강제로 종료"
    public void OnSensorHide()
    {
        // 1) 현재 흡연/게이지/연기/이모티콘/코루틴 모두 정리
        ForceStopAllSmoking();
        ClearOuterAngryState();

        // 2) 엔딩 플래그 초기화
        endingActivated = false;

        // 3) 이 존 로직을 잠시 끔 (Update / CheckEndingZone이 더 이상 돌지 않게)
        this.enabled = false;
    }

    // 센서 B: "ending2가 이 구역으로 와서 엔딩을 열 준비" 
    public void OnSensorShow()
    {
        // 1) 이 스크립트를 다시 활성화 (Update / CheckEndingZone 재가동)
        this.enabled = true;

        // 2) 엔딩 플래그 초기화 (새 엔딩을 다시 받을 수 있게)
        endingActivated = false;

        // ★ 이 존에서 EndCount 다시 증가 가능하도록 리셋
        endCountGiven = false;

        // 3) ending2(EndingPoint)를 이 IllegalSmoking 위치로 이동
        if (endingPoint != null)
        {
            endingPoint.position = transform.position;
        }
        else
        {
            Debug.LogWarning("[IllegalSmoking] endingPoint가 설정되지 않았습니다. Inspector에서 Ending2 오브젝트를 연결하세요.");
        }

        // 4) 게이지도 초기화
        if (discomfortGaugeObject != null)
            discomfortGaugeObject.SetActive(false);

        if (discomfortSlider != null)
        {
            discomfortSlider.value = 0f;
            if (sliderFillImage != null)
                sliderFillImage.color = normalColor;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, endRadius);

        // 외곽 감지 영역 Gizmo (파란색)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, radius * angryOuterRadiusMultiplier);
    }

    private void OnDisable()
    {
        foreach (var kv in spawnedEmojis)
            RemoveEmojisFor(kv.Key);
        spawnedEmojis.Clear();
        frozenYPositions.Clear();

        animatorDiscomfortTimers.Clear();
        animatorAngryEmojiTimers.Clear();
        pendingCoroutines.Clear();
        insideAnimators.Clear();

        // 외곽 상태 정리
        ClearOuterAngryState();

        // 연기도 정리
        if (smokeEffect != null)
        {
            smokeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        isSmokePlaying = false;
    }
}