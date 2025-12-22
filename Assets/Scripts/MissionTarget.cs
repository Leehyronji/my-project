using UnityEngine;
using UnityEngine.Video; // 비디오 기능 추가

public class MissionTarget : MonoBehaviour
{
    [Header("변환된 영상 넣기")]
    public VideoClip videoClip; // 파일 이름(String) 대신 비디오클립(VideoClip)을 직접 받음

    void OnDisable()
    {
        if (gameObject.scene.isLoaded && GameManager.Instance != null)
        {
            // 매니저에게 비디오 클립을 통째로 전달
            GameManager.Instance.PlayVideo(videoClip);
        }
    }
}