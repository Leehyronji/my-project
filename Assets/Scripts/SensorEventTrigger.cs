using UnityEngine;

public class SensorEventTrigger : MonoBehaviour
{
    [Header("Sensor Source")]
    public ArduinoSensorReader sensorReader;

    [Header("Target Animator")]
    public Animator targetAnimator;
    public string triggerName_IR = "OnIR";         // IR 센서용 트리거 이름
    public string triggerName_Press = "OnPress";   // 압력 센서용 트리거 이름

    private bool lastIrDetected = false;
    private bool lastPressureDetected = false;

    void Update()
    {
        if (sensorReader == null) return;

        // IR: OFF -> ON 변화 순간 감지
        if (!lastIrDetected && sensorReader.irDetected)
        {
            OnIrTriggered();
        }

        // PRESS: OFF -> ON 변화 순간 감지
        if (!lastPressureDetected && sensorReader.pressureDetected)
        {
            OnPressureTriggered();
        }

        // 상태 업데이트
        lastIrDetected = sensorReader.irDetected;
        lastPressureDetected = sensorReader.pressureDetected;
    }

    void OnIrTriggered()
    {
        Debug.Log("[SensorEventTrigger] IR Triggered!");
        if (targetAnimator != null && !string.IsNullOrEmpty(triggerName_IR))
        {
            targetAnimator.SetTrigger(triggerName_IR);
        }
    }

    void OnPressureTriggered()
    {
        Debug.Log("[SensorEventTrigger] Pressure Triggered!");
        if (targetAnimator != null && !string.IsNullOrEmpty(triggerName_Press))
        {
            targetAnimator.SetTrigger(triggerName_Press);
        }
    }
}