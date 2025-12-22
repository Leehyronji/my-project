using UnityEngine;

public class EndEventCounter : MonoBehaviour
{
    [Header("Animator Parameter Names")]
    public string endCountParamName = "EndCount";
    public string finalIndexParamName = "FinalIndex";
    public string finalStartTriggerName = "FinalStart";

    [Header("Final Animation Count")]
    public int finalAnimationCount = 11;

    private Animator animator;
    private int endCountHash;
    private int finalIndexHash;
    private int finalStartHash;

    // ★ 엔딩 연출이 이미 실행되었는지 여부
    private bool finalPlayed = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[EndEventCounter] Animator를 찾을 수 없습니다.");
            return;
        }

        endCountHash   = Animator.StringToHash(endCountParamName);
        finalIndexHash = Animator.StringToHash(finalIndexParamName);
        finalStartHash = Animator.StringToHash(finalStartTriggerName);
    }

    void Update()
    {
        CheckFinalCondition();
    }

    private void CheckFinalCondition()
    {
        if (animator == null) return;
        if (finalPlayed) return;  // ★ 이미 한 번 실행했으면 더 이상 진입 금지

        int count = animator.GetInteger(endCountHash);

        // 엔딩카운트
        if (count >= 6)
        {
            Debug.Log($"[EndEventCounter] EndCount = {count}, Final Animation 실행!");

            finalPlayed = true;   // ★ 여기서 한 번만 true로

            PauseAllWalkers();
            FreezeAllRigidbodies();
            FixAllYPositions();   // 필요 없으면 이 호출 자체를 제거해도 됨

            PlayRandomFinalAnimation();
        }
    }

    // ★ 모든 NPC 이동 멈춤
    private void PauseAllWalkers()
    {
        WaypointWalker[] walkers = FindObjectsOfType<WaypointWalker>();
        foreach (var walker in walkers)
        {
            walker.PauseWalking();
        }

        Debug.Log($"[EndEventCounter] 전체 NPC {walkers.Length}명 PauseWalking()");
    }

    // ★ 모든 NPC Rigidbody 회전 잠금 + 힘 전달 차단
    private void FreezeAllRigidbodies()
    {
        Rigidbody[] rbs = FindObjectsOfType<Rigidbody>();

        foreach (var rb in rbs)
        {
            rb.isKinematic = true;      // 물리 영향 차단
            rb.freezeRotation = true;   // 회전 고정

            // 필요하면 위치까지 완전 고정하고 싶을 때:
            // rb.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
        }

        Debug.Log($"[EndEventCounter] Rigidbody {rbs.Length}개 회전/물리 고정");
    }

    // ★ 모든 NPC Y값 고정(실제론 '현재 값 유지'만 수행, 재배치 X)
    private void FixAllYPositions()
    {
        Animator[] anims = FindObjectsOfType<Animator>();

        foreach (Animator anim in anims)
        {
            Transform t = anim.transform;

            // ★ 현재 y를 다시 넣는 건 사실상 no-op이지만,
            //    "지금 이 위치를 기준으로 더 이상 움직이고 싶지 않다"는 의도를 명시적으로 남김.
            float currentY = t.position.y;
            t.position = new Vector3(t.position.x, currentY, t.position.z);
        }

        Debug.Log("[EndEventCounter] 모든 Animator Y축 현재 값 유지 (재배치 없음)");
    }

    private void PlayRandomFinalAnimation()
    {
        if (animator == null) return;
        if (finalAnimationCount <= 0)
        {
            Debug.LogWarning("[EndEventCounter] finalAnimationCount가 0 이하입니다.");
            return;
        }

        int idx = Random.Range(0, finalAnimationCount);
        animator.SetInteger(finalIndexHash, idx);
        animator.SetTrigger(finalStartHash);

        Debug.Log($"[EndEventCounter] FinalIndex = {idx}, FinalStart Trigger fired.");
    }

    public void ResetEndCount()
    {
        if (animator == null) return;
        animator.SetInteger(endCountHash, 0);
        finalPlayed = false;  // ★ 다시 엔딩 가능하게
    }
}