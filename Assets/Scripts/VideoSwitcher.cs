using UnityEngine;
using UnityEngine.Video;

public class VideoSwitcher : MonoBehaviour
{
    [Header("필수 연결")]
    public VideoPlayer videoPlayer;   // Video Player 컴포넌트

    [Header("영상 시퀀스 (m1 → m2 → m3 → ending)")]
    public VideoClip m1Clip;
    public VideoClip m2Clip;
    public VideoClip m3Clip;
    public VideoClip endingClip;

    [Tooltip("시작할 때 자동으로 m1을 재생할지 여부")]
    public bool playM1OnStart = true;

    [Tooltip("ending이 아닐 때는 loop, ending일 때는 loop 끄기")]
    public bool loopNonEnding = true;

    // 0: m1, 1: m2, 2: m3, 3: ending
    private int currentIndex = 0;

    void Start()
    {
        if (playM1OnStart)
        {
            PlayByIndex(0);
        }
    }

    /// <summary>
    /// 센서에서 "한 번" 값이 올 때마다 호출 → m1→m2→m3→ending 순으로 한 단계 진행
    /// </summary>
    public void AdvanceSequence()
    {
        if (videoPlayer == null)
        {
            Debug.LogWarning("[VideoSwitcher] VideoPlayer가 설정되지 않았습니다.");
            return;
        }

        // 이미 ending까지 온 경우 더 이상 진행하지 않음
        if (currentIndex >= 3)
        {
            Debug.Log("[VideoSwitcher] 이미 ending 영상까지 재생 완료. 더 이상 진행하지 않습니다.");
            return;
        }

        int nextIndex = currentIndex + 1;
        PlayByIndex(nextIndex);
    }

    /// <summary>
    /// 필요 시 외부에서 호출해서 m1부터 다시 시작하고 싶을 때 사용
    /// </summary>
    public void ResetSequenceToM1()
    {
        PlayByIndex(0);
    }

    private void PlayByIndex(int index)
    {
        if (videoPlayer == null)
        {
            Debug.LogWarning("[VideoSwitcher] VideoPlayer가 설정되지 않았습니다.");
            return;
        }

        VideoClip clip = null;

        switch (index)
        {
            case 0: clip = m1Clip; break;
            case 1: clip = m2Clip; break;
            case 2: clip = m3Clip; break;
            case 3: clip = endingClip; break;
        }

        if (clip == null)
        {
            Debug.LogWarning($"[VideoSwitcher] index {index}에 해당하는 VideoClip이 비어 있습니다.");
            return;
        }

        currentIndex = index;
        videoPlayer.clip = clip;

        // ending이면 loop 끄고, 나머지는 loop 옵션대로
        if (index == 3)
            videoPlayer.isLooping = false;
        else
            videoPlayer.isLooping = loopNonEnding;

        videoPlayer.Play();
        Debug.Log($"[VideoSwitcher] Video index {index} 재생 시작: {clip.name}");
    }
}