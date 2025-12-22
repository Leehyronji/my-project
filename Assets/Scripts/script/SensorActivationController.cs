using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SensorActivationController : MonoBehaviour
{
    public enum MoveMode
    {
        ObjectToEnding,   // 오브젝트 → endingPoint 위치로 이동 (trash, car)
        EndingToObject    // endingPoint → 오브젝트 위치로 이동 (IllegalSmoking)
    }

    [Header("센서 이벤트를 처리할 로직 (trash1, event_car, IllegalSmoking 등)")]
    public MonoBehaviour logicComponent;
    private ISensorReactiveEvent logic;

    [Header("아두이노 센서 연결")]
    public ArduinoSensorReader hideSensor;
    public ArduinoSensorReader showSensor;

    [Header("오브젝트/엔딩 이동 설정")]
    public MoveMode moveMode = MoveMode.ObjectToEnding;   // 기본: 오브젝트가 엔딩으로 이동
    public Transform endingPoint;                         // ending0, ending1, SmokingArea 등

    [Header("가시성 제어 대상")]
    public Renderer[] targetRenderers;
    public Collider[] targetColliders;

    private bool lastHideIrState = false;
    private bool lastShowIrState = false;

    void Awake()
    {
        if (logicComponent != null)
        {
            logic = logicComponent as ISensorReactiveEvent;
            if (logic == null)
            {
                Debug.LogError("[SensorActivationController] logicComponent가 ISensorReactiveEvent를 구현하지 않습니다.");
            }
        }
        else
        {
            Debug.LogError("[SensorActivationController] logicComponent가 설정되지 않았습니다.");
        }

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>();

        if (targetColliders == null || targetColliders.Length == 0)
            targetColliders = GetComponentsInChildren<Collider>();
    }

    void Start()
    {
        SetVisible(true);
    }

    void Update()
    {
        HandleHideSensor();
        HandleShowSensor();
    }

    // -------------------------
    // 센서 A: hide
    // -------------------------
    private void HandleHideSensor()
    {
        if (hideSensor == null || logic == null)
            return;

        bool current = hideSensor.irDetected;

        // 예) ON → OFF 엣지 등, 주인님이 정의한 기준에 맞춰 사용
        if (lastHideIrState && !current)
        {
            Debug.Log("[SensorActivationController] Hide sensor edge (ON → OFF)");

            // 1) 로직에 hide 알림
            logic.OnSensorHide();

            // 2) 오브젝트 숨기기
            SetVisible(false);
        }

        lastHideIrState = current;
    }

    // -------------------------
    // 센서 B: show
    // -------------------------
    private void HandleShowSensor()
    {
        if (showSensor == null || logic == null)
            return;

        bool current = showSensor.irDetected;

        // 예) OFF → ON 엣지
        if (!lastShowIrState && current)
        {
            Debug.Log("[SensorActivationController] Show sensor edge (OFF → ON)");

            // 1) 이동 방향에 따라 이동 적용
            ApplyMovement();

            // 2) 다시 보이게
            SetVisible(true);

            // 3) 로직에 show 알림
            logic.OnSensorShow();
        }

        lastShowIrState = current;
    }

    // -------------------------
    // 이동 처리 (핵심 부분)
    // -------------------------
    private void ApplyMovement()
    {
        if (endingPoint == null)
        {
            Debug.LogWarning("[SensorActivationController] endingPoint가 설정되지 않았습니다.");
            return;
        }

        switch (moveMode)
        {
            case MoveMode.ObjectToEnding:
                // trash / car: 이 오브젝트가 endingPoint 위치로 이동
                transform.position = endingPoint.position;
                break;

            case MoveMode.EndingToObject:
                // IllegalSmoking: endingPoint(SmokingArea)가 이 오브젝트 위치로 이동
                endingPoint.position = transform.position;
                break;
        }
    }

    // -------------------------
    // 가시성 제어
    // -------------------------
    private void SetVisible(bool visible)
    {
        if (targetRenderers != null)
        {
            foreach (var r in targetRenderers)
            {
                if (r != null) r.enabled = visible;
            }
        }

        if (targetColliders != null)
        {
            foreach (var c in targetColliders)
            {
                if (c != null) c.enabled = visible;
            }
        }
    }
}