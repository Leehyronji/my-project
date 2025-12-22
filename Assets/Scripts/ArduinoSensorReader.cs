using UnityEngine;
using System.Threading;
using System.IO.Ports;

public class ArduinoSensorReader : MonoBehaviour
{
    [Header("Serial Settings")]
    public bool useSerial = false;      // 아두이노 연결 시 체크
    public string portName = "/dev/cu.usbmodem1110";    // 포트 이름 (맥은 /dev/cu.usbmodem... 등)
    public int baudRate = 9600;

    private SerialPort serialPort;
    private Thread readThread;
    private bool isRunning = false;

    [Header("Detection Settings")]
    [Tooltip("센서가 이 시간(초) 이상 지속적으로 감지되어야 인식이 확정됩니다.")]
    public float detectionTime = 1.0f;  // 1초 동안 유지되어야 함

    // 내부 타이머 변수
    private float irTimer = 0f;
    private float pressureTimer = 0f;

    // 아두이노에서 날아온 '생' 데이터 (즉시 반응)
    private bool rawIrInput = false;
    private bool rawPressureInput = false;

    [Header("Final Sensor States (Read Only)")]
    // 1초가 지나서 최종 확정된 상태 (다른 스크립트는 이것만 보면 됨)
    public bool irDetected = false;
    public bool pressureDetected = false;

    // 직전 프레임 값 (엣지 검출용)
    private bool prevIrDetected = false;
    private bool prevPressureDetected = false;

    // ============================
    // 단 한 번만 시퀀스를 보내기 위한 플래그
    // ============================
    private bool sequenceTriggeredOnce = false;

    [Header("Simulation Settings")]
    public bool simulateInEditor = true;
    public KeyCode simulateIrKey = KeyCode.Alpha1;
    public KeyCode simulatePressKey = KeyCode.Alpha2;

    // ============================
    // 시퀀스 전달 설정
    // ============================
    [Header("Sequence Trigger Options")]
    [Tooltip("IR 감지(확정) 시 AdvanceSequence()를 호출할지 여부")]
    public bool triggerOnIr = true;

    [Tooltip("압력 감지(확정) 시 AdvanceSequence()를 호출할지 여부")]
    public bool triggerOnPressure = false;

    [System.Serializable]
    public class SequenceTarget
    {
        [Tooltip("이 Target에 AdvanceSequence를 보낼지 여부")]
        public bool send = true;

        [Tooltip("시퀀스를 진행시킬 VideoSwitcher (또는 다른 시퀀스 스크립트)")]
        public VideoSwitcher videoSwitcher;
    }

    [Header("AdvanceSequence를 보낼 대상 리스트")]
    public SequenceTarget[] sequenceTargets;

    void Start()
    {
        if (useSerial)
        {
            OpenPort();
        }
    }

    void Update()
    {
        // 1. 에디터 시뮬레이션 입력 처리 (시리얼 미사용 시)
        if (simulateInEditor && !useSerial)
        {
            rawIrInput = Input.GetKey(simulateIrKey);
            rawPressureInput = Input.GetKey(simulatePressKey);
        }

        // 2. IR 센서 타이머 로직 (1초 체크)
        if (rawIrInput)
        {
            irTimer += Time.deltaTime;
            if (irTimer >= detectionTime)
            {
                irDetected = true;
            }
        }
        else
        {
            irTimer = 0f;
            irDetected = false;
        }

        // 3. 압력 센서 타이머 로직 (1초 체크)
        if (rawPressureInput)
        {
            pressureTimer += Time.deltaTime;
            if (pressureTimer >= detectionTime)
            {
                pressureDetected = true;
            }
        }
        else
        {
            pressureTimer = 0f;
            pressureDetected = false;
        }

        // 4. 엣지(0→1) 발생 시에만 시퀀스 진행
        //    - IR
        if (triggerOnIr && irDetected && !prevIrDetected)
        {
            OnIrConfirmed();
        }

        //    - Pressure
        if (triggerOnPressure && pressureDetected && !prevPressureDetected)
        {
            OnPressureConfirmed();
        }

        // 직전 값 갱신
        prevIrDetected = irDetected;
        prevPressureDetected = pressureDetected;
    }

    // ============================
    // 시퀀스 진행 트리거 진입점
    // ============================

    private void OnIrConfirmed()
    {
        // 이미 한 번이라도 시퀀스를 보냈다면 무시
        if (sequenceTriggeredOnce)
            return;

        sequenceTriggeredOnce = true;
        AdvanceSequence();
    }

    private void OnPressureConfirmed()
    {
        // 이미 한 번이라도 시퀀스를 보냈다면 무시
        if (sequenceTriggeredOnce)
            return;

        sequenceTriggeredOnce = true;
        AdvanceSequence();
    }

    /// <summary>
    /// 센서 확정 신호가 들어왔을 때 호출되는 공통 진입점.
    /// 인스펙터에서 send = true로 체크된 타겟에만 AdvanceSequence()를 전달.
    /// </summary>
    public void AdvanceSequence()
    {
        if (sequenceTargets == null || sequenceTargets.Length == 0)
        {
            Debug.LogWarning("[ArduinoSensorReader] sequenceTargets가 비어 있어 AdvanceSequence를 보낼 대상이 없습니다.");
            return;
        }

        foreach (var target in sequenceTargets)
        {
            if (target == null) continue;
            if (!target.send) continue;              // 체크 안 된 대상은 스킵
            if (target.videoSwitcher == null) continue;

            target.videoSwitcher.AdvanceSequence();
        }
    }

    // ============================
    // 시리얼 포트 관련
    // ============================
    void OpenPort()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 50;
            serialPort.Open();

            isRunning = true;
            readThread = new Thread(ReadSerialLoop);
            readThread.Start();

            Debug.Log("[ArduinoSensorReader] Serial Port Opened: " + portName);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ArduinoSensorReader] Failed to open serial port: " + e.Message);
        }
    }

    // 에러 무시 로직이 추가된 시리얼 읽기 루프
    void ReadSerialLoop()
    {
        while (isRunning)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    try
                    {
                        string line = serialPort.ReadLine();
                        ParseLine(line);
                    }
                    catch (System.Exception ex)
                    {
                        if (ex.Message.Contains("Resource temporarily unavailable"))
                        {
                            // 맥에서 자주 뜨는 에러는 무시
                        }
                        else if (ex is System.TimeoutException)
                        {
                            // 타임아웃은 정상 상황
                        }
                        else
                        {
                            Debug.LogWarning("[ArduinoSensorReader] Serial read error: " + ex.Message);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Thread Error] " + e.Message);
            }

            Thread.Sleep(1);
        }
    }

    void ParseLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return;

        line = line.Trim();
        
        // 아두이노가 보내주는 데이터 "IR:1" 또는 "PRESS:1" 등을 해석
        if (line.StartsWith("IR:"))
        {
            string valueStr = line.Substring(3);
            if (int.TryParse(valueStr, out int value))
            {
                rawIrInput = (value == 1);
            }
        }
        else if (line.StartsWith("PRESS:"))
        {
            string valueStr = line.Substring(6);
            if (int.TryParse(valueStr, out int value))
            {
                rawPressureInput = (value == 1);
            }
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(100);
        }

        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }
    }
}