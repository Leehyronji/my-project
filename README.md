# CAU-AnT-CAP1-산악회
중앙대학교 예술공학부 캡스톤1 디자인 프로젝트 (2025)

## Project Overview
본 프로젝트는 관객의 물리적 개입을 실시간 입력값으로 받아, 디지털 캐릭터의 감정 상태와 서사 흐름이 즉각적으로 변화하도록 설계된 인터랙티브 미디어 아트 작품이다.
Unity 엔진을 기반으로 한 실시간 시뮬레이션 로직과 TouchDesigner를 활용한 프로젝션 맵핑 시스템을 결합함으로써,
고정된 디스플레이에 국한되지 않고 비정형 전시 공간에서도 작동 가능한 몰입형 인터랙션 환경을 구현하는 것을 목표로 하였다.

본 작업은 단순한 관객 반응형 콘텐츠를 넘어, 관객의 개입이 시스템 전반의 상태(State)에 누적적으로 영향을 미치고
그 결과가 시각·청각·서사적 요소로 통합적으로 표현되는 구조를 지향한다.

---

## Team
**팀명:** 산악회

팀원 : 강현규(20212215), 김채영(20230032), 이현지(20231841), 조하은(20222678), 박준영(20226669)

---

## Project Goals

### Technical Goals
- Unity 실시간 렌더링 화면을 **지연 없이 TouchDesigner로 송출**
- Spout / NDI 기반 멀티 디스플레이 파이프라인 안정화
- 비정형 디스플레이(프로젝션 맵핑) 환경에서도 로직 완전 동작

### Content Goals
- ‘군중 속 차량’이라는 상황 설정
- 관객 또는 NPC의 방해에 따라 **차량의 감정 상태가 변화**
- 감정 변화를 **게이지(UI), 이모티콘(3D), 사운드(경적)**로 직관적으로 표현

---

## Core Concept & Features

본 프로젝트의 핵심 개념은 **“개입에 의해 변화하는 감정 상태”**이다.

차량 캐릭터는 단순한 오브젝트가 아닌,
주변 환경과 타인의 행동에 반응하는 하나의 에이전트로 설정된다.
관객 또는 NPC가 차량의 이동을 방해할수록,
차량의 감정 수치는 점진적으로 변화하며 이는 다음 요소로 시각화된다.

- 상태 게이지(UI)
- 3D 이모티콘 오브젝트
- 사운드(경적 및 음향 변화)
이러한 반응은 일회성 이벤트가 아니라 누적 기반으로 관리되며,
특정 임계값 도달 시 엔딩 시퀀스로 자연스럽게 전환된다.

- **Real-time Pipeline**
  - Unity (Logic & Simulation)  
  - → Spout / NDI  
  - → TouchDesigner (Final Output)

- **Dual View System**
  - 프로젝터: 맵핑된 메인 콘텐츠
  - 모니터/TV: 상태 UI 또는 서브 화면

- **Reactive Interaction**
  - 장애물(사람) 감지 시
    - 차량 정지
    - 불쾌함 게이지 상승
    - 화난 이모티콘 생성
    - 사운드 변화

---

## System Architecture

### Unity
#### 1. Dynamic NPC Interaction
- `Physics.OverlapSphere` 기반 **구역 감지**
- `LayerMask`를 활용한 이벤트 대상 분리
- `HashSet` 기반 진입/이탈 Diff 처리

#### 2. NPC Behavior Control
- `NavMeshAgent` 이동 강제 중단
- State Machine 기반 애니메이션 전환
- `Rigidbody.isKinematic` 제어로 물리 충돌 방지

#### 3. Visual Feedback System
- NPC 머리 위를 추적하는 **3D 이모티콘**
- 체류 시간 기반 **Discomfort Gauge UI**

#### 4. Multi-Display Orchestration
- `Display.displays[1].Activate()`로 듀얼 디스플레이 분리
- Arduino 센서 입력 → 영상 클립 즉시 전환

#### 5. Ending Sequence Automation
- 이벤트 해결 횟수 누적 → 엔딩 자동 트리거
- 씬 전체 NPC/물리 연산 동결
- 11종 랜덤 엔딩 연출

---

### Vehicle NPC Stabilization
- Waypoint 기반 자율 주행
- `Vector3.MoveTowards` + Snap-to-Target 전략
- 회전 오버슈트 및 진동 현상 제거
- Raycast 기반 전방 장애물 감지

---

## Arduino Integration

### Hardware
- Arduino Uno (R3)
- IR Obstacle Sensor

### Communication
- USB Serial (9600 baud)

### Protocol
- 감지 시: `IR:1`
- 미감지 시: `IR:0`
- 100ms 주기 송신

### Unity Side
- 비동기 시리얼 통신 (Thread 기반)
- MacOS 예외 처리
- 디바운싱 + 지속 시간 필터링
- `ISensorReactiveEvent` 인터페이스로 이벤트 확산

---

## Exhibition Setup

- **Main PC**
  - Unity + TouchDesigner
- **Display 1**
  - 프로젝터 맵핑 화면
- **Display 2**
  - 클린 게임 뷰
- 천장 설치로 그림자 최소화

### Results
- 장시간 전시 중 안정적 구동
- 듀얼 디스플레이 좌표 이슈 해결

---

## Future Work
- IR 센서 → 카메라 기반 객체 추적 확장
- XR 환경으로 확장 가능한 인터랙션 구조
- 멀티 디스플레이 전용 빌드 파이프라인 구축

---

## Significance
본 프로젝트는 **현실 입력(Arduino)–가상 에이전트(NPC/차량)–멀티 디스플레이–외부 미디어 송출–엔딩 전역 제어**가  
단일 파이프라인으로 통합된 **인터랙티브 디지털 트윈 시스템**의 구현 사례이다.

실제 구현 과정에서 발생한 회전 불안정, NDI 스트리밍 오류 등을 구조적으로 해결한 경험은  
향후 대규모 실시간 전시 및 시뮬레이션 환경으로 확장 가능한 설계 자산으로 작용한다.
