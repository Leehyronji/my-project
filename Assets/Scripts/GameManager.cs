using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("TV 연결 설정")]
    public RawImage tvRawImage;
    public VideoPlayer tvVideoPlayer;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        if (Display.displays.Length > 1)
            Display.displays[1].Activate();

        if (tvVideoPlayer != null)
        {
            tvVideoPlayer.prepareCompleted += (source) =>
            {
                tvRawImage.texture = source.texture;
                tvRawImage.color = Color.white; // 영상 준비되면 화면 켜기
            };
        }
    }

    // [수정됨] 파일 이름 대신 VideoClip을 받도록 변경
    public void PlayVideo(VideoClip clip)
    {
        if (tvVideoPlayer != null && clip != null)
        {
            Debug.Log($"영상 재생 시작: {clip.name}");
            
            // URL 모드가 아니라 Clip 모드로 변경
            tvVideoPlayer.source = VideoSource.VideoClip; 
            tvVideoPlayer.clip = clip;
            
            tvVideoPlayer.Prepare();
            tvVideoPlayer.Play();
        }
    }
}