using UnityEngine;

public interface ISensorReactiveEvent
{
    // 센서 A: 물체 사라질 때 호출 (EventOff 쏘고, 내부 상태 정리, 필요하면 자기 자신 비활성화)
    void OnSensorHide();

    // 센서 B: 물체 새로 감지될 때 호출 (엔딩 위치 이후 상태 준비, 자기 자신 활성화 등)
    void OnSensorShow();
}
