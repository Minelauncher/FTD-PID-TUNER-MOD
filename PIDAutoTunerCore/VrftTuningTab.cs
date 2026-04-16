// ============================================================================
// VrftTuningTab.cs — VRFT 기반 PID 자동 튜닝 UI 탭 (전체 핵심 로직)
//
// ■ using 설명 (C# 기본):
//   using = "이 네임스페이스의 클래스를 쓰겠다"는 선언.
//   Java의 import, Python의 from X import * 와 비슷.
//
// ============================================================================
// ■ 메서드/타입 출처 레퍼런스 (코드 읽을 때 참고)
//
// ── 출처 범례 ──
//   [C#]     = C# / .NET 기본 라이브러리
//   [FTD]    = FTD 게임 DLL (BrilliantSkies.*)
//   [Unity]  = Unity 엔진 (UnityEngine.*)
//   [MathNet]= MathNet.Numerics 라이브러리
//   [자체]   = 이 모드에서 직접 만든 코드
//
// ── 이 파일의 메서드들 ──
//
//   [자체] VrftTuningTab(window, focus)     생성자. FTD SuperScreen 상속
//   [자체] Build()                          override. UI 요소 배치 (FTD가 호출)
//   [자체] OnUiFixed()                      매 물리 틱 호출. 데이터 수집/적응형 진폭
//   [자체] BuildStatus()                    UI: 상태 표시 영역 생성
//   [자체] BuildSettingsSliders()            UI: 설정 슬라이더들 생성
//   [자체] BuildExcitationControls()         UI: 가진 설정 영역 생성
//   [자체] BuildActionButtons()              UI: 버튼들 (자동튜닝/녹화/계산/적용)
//   [자체] BuildResult()                    UI: 결과 표시 영역
//   [자체] StartRecording()                 녹화 시작 (세션 초기화)
//   [자체] StopRecording()                  녹화 중지 (SP 복원)
//   [자체] AutoTuneNow()                    [자동 튜닝] 버튼 → 가진 설정 + 녹화 시작
//   [자체] AutoTuneCompute()                녹화 완료 후 → 추정 + VRFT 계산
//   [자체] CaptureSetPointAdjustBase()      현재 SP 백업
//   [자체] RestoreSetPointAdjustIfNeeded()   SP를 원래 값으로 복원
//   [자체] ApplyExcitation(dt)              매 틱 SP에 가진 신호 더하기
//   [자체] ComputeNow()                     [계산] 버튼 → VRFT 계산 (수동)
//   [자체] ApplyToPid()                     [적용] 버튼 → 결과를 게임 PID에 쓰기
//   [자체] ComputeVrftPid(u,y,dt,s)         ★ VRFT 핵심 계산 (static)
//   [자체] MakeButton(...)                  UI 헬퍼: 버튼 생성
//   [자체] MakeToggle(...)                  UI 헬퍼: 토글 생성
//   [자체] MakeCycleButton(...)             UI 헬퍼: 순환 버튼 생성
//   [자체] MakeSliderFloat(...)             UI 헬퍼: float 슬라이더 생성
//   [자체] MakeSliderInt(...)               UI 헬퍼: int 슬라이더 생성
//   [자체] WaveToKo(w)                      WaveType → 한국어 문자열
//   [자체] Clamp(v,lo,hi)                   값 범위 제한 (float)
//   [자체] ClampInt(v,lo,hi)                값 범위 제한 (int)
//   [자체] RoundToStep(v,step)              step 단위로 반올림
//   [자체] NextPow2(n)                      2의 거듭제곱으로 올림
//   [자체] Detrend(x)                       DC+선형추세 제거 (in-place)
//   [자체] PickLongestBlock(starts,total)   블록 리스트에서 최장 구간 찾기
//   [자체] StdDev(data)                     표준편차 계산
//   [자체] EstimateDelay(u,y,dt)            임펄스 응답 기반 지연 추정
//   [자체] EstimateSettlingTime(y,dt)       자기상관 기반 정착시간 추정
//
// ── 사용하는 외부 타입/메서드 ──
//
//   [FTD]    SuperScreen<T>                 UI 탭 기본 클래스 (상속)
//   [FTD]    VariableControllerMaster       PID 제어기 객체 (this._focus)
//   [FTD]    IVariableController            개별 제어 채널 인터페이스
//     .GetCurrentController()               [FTD] 현재 활성 컨트롤러 반환
//     .LastControlVariable                  [FTD] 마지막 제어 출력 (u)
//     .LastProcessVariable                  [FTD] 마지막 프로세스 변수 (y)
//   [FTD]    this._focus.SetPointAdjust.Us  SetPoint 오프셋 (읽기/쓰기)
//   [FTD]    this._focus.Pid.kP.Us          PID의 Kp 값 (읽기/쓰기)
//   [FTD]    this._focus.Pid.kI.Us          PID의 Ti 값 (읽기/쓰기)
//   [FTD]    this._focus.Pid.kD.Us          PID의 Td 값 (읽기/쓰기)
//   [FTD]    ConsoleWindow                  UI 창 객체
//   [FTD]    ScreenSegmentStandard          UI 구획 (세로)
//   [FTD]    ScreenSegmentTable             UI 구획 (테이블)
//   [FTD]    ScreenSegmentStandardHorizontal UI 구획 (가로)
//   [FTD]    SubjectiveDisplay<T>           읽기 전용 텍스트 표시
//   [FTD]    SubjectiveButton<T>            클릭 버튼
//   [FTD]    SubjectiveToggle<T>            ON/OFF 토글
//   [FTD]    SubjectiveFloatClampedWithBar<T> 슬라이더 (범위 제한 float)
//   [FTD]    M.m<T>(값)                     매 프레임 값을 갱신하는 래퍼 (UI용)
//   [FTD]    Content(텍스트, 툴팁, 태그)    탭/UI 이름 + 설명 묶음
//   [FTD]    ToolTip(텍스트, 폭)            마우스 올리면 나오는 설명
//   [FTD]    InsertPosition.OnCursor        UI 요소 삽입 위치
//   [FTD]    ConsoleStyles.Instance          UI 스타일 싱글톤
//   [FTD]    base.CreateStandardSegment()   부모(SuperScreen)의 UI 구획 생성
//   [FTD]    base.CreateTableSegment(열,행) 부모(SuperScreen)의 테이블 생성
//   [FTD]    base.CreateStandardHorizontalSegment() 가로 구획 생성
//   [FTD]    seg.AddInterpretter(...)       구획에 UI 요소 추가
//
//   [Unity]  Time.fixedDeltaTime            물리 틱 간격 (보통 0.02초)
//
//   [MathNet] Fourier.Forward(data, opt)    FFT (시간→주파수)
//   [MathNet] Fourier.Inverse(data, opt)    IFFT (주파수→시간)
//   [MathNet] FourierOptions.Matlab         FFT 스케일링 옵션 (MATLAB 호환)
//   [MathNet] Matrix<double>.Build.Dense()  행렬 생성
//   [MathNet] Vector<double>.Build.DenseOfArray() 벡터 생성
//   [MathNet] matrix.Svd()                  특이값 분해 (SVD)
//   [MathNet] matrix.QR().Solve(b)          QR 분해 → 최소자승 풀기
//   [MathNet] svd.S                         특이값 벡터
//   [MathNet] svd.VT                        V 전치 행렬
//
//   [C#]     Math.Sin/Cos/Sqrt/Abs/...      기본 수학 함수
//   [C#]     Math.Clamp(v,min,max)          범위 제한
//   [C#]     Complex                        복소수 (실수부 + 허수부)
//   [C#]     Complex.Exp/Pow/Conjugate      복소수 연산
//   [C#]     Array.Copy(src,dst,len)        배열 복사
//   [C#]     List<T>.Add/Clear/Count/CopyTo 리스트 조작
//   [C#]     string.IsNullOrEmpty(s)        null 또는 빈 문자열 체크
//   [C#]     $"...{변수}..."                문자열 보간 (f-string과 동일)
//   [C#]     () => 표현식                   람다 (Python의 lambda와 동일)
//   [C#]     Action<T> / Func<T>            함수를 변수로 전달하는 타입
//   [C#]     try/catch                      예외 처리
//   [C#]     double.IsNaN/IsInfinity        숫자 유효성 체크
// ============================================================================

using System;                          // 기본 타입 (Math, Array, Exception 등)
using System.Collections.Generic;      // List<T>, Dictionary 등 컬렉션
using System.Linq;                     // Enumerable (ERA에서 사용)
using System.Numerics;                 // Complex (복소수) — FFT 계산에 필수
using BrilliantSkies.Ai.Control.Pids;  // FTD PID 관련 (VariableControllerMaster, IVariableController 등)
using BrilliantSkies.Core.Control.Tuning; // IAccelerationMeasurement
using BrilliantSkies.Ui.Consoles;      // FTD UI 시스템 (ConsoleWindow, SuperScreen 등)
using BrilliantSkies.Ui.Consoles.Getters;                          // M.m<T> — UI 값 갱신 래퍼
using BrilliantSkies.Ui.Consoles.Interpretters.Subjective;         // SubjectiveDisplay 등
using BrilliantSkies.Ui.Consoles.Interpretters.Subjective.Buttons; // SubjectiveButton
using BrilliantSkies.Ui.Consoles.Interpretters.Subjective.Choices; // SubjectiveToggle
using BrilliantSkies.Ui.Consoles.Interpretters.Subjective.Numbers; // SubjectiveFloatClampedWithBar (슬라이더)
using BrilliantSkies.Ui.Consoles.Segments;  // ScreenSegmentStandard 등 (UI 구획)
using BrilliantSkies.Ui.Consoles.Styles;    // ConsoleStyles (UI 스타일/테마)
using BrilliantSkies.Ui.Tips;               // ToolTip (마우스 올리면 나오는 설명)
using MathNet.Numerics.IntegralTransforms;  // Fourier (FFT/IFFT)
using MathNet.Numerics.LinearAlgebra;       // Matrix, Vector (선형대수 — SVD, QR 등)
using UnityEngine;                          // Time.fixedDeltaTime (Unity 물리 틱 간격)

namespace PIDAutoTuner
{
    /// <summary>
    /// VRFT(Virtual Reference Feedback Tuning) 기반 PID 자동 튜닝 UI 탭.
    ///
    /// ■ VRFT가 뭔가?
    ///   플랜트(제어 대상)의 수학적 모델 없이, 입출력 데이터(u, y)만으로
    ///   "원하는 폐루프 응답 M"을 만족하는 PID 파라미터를 한 번에 계산하는 방법.
    ///
    /// ■ 핵심 수식 (논문 번호):
    ///   (3.0.17) M(jw) = exp(-jw*tau_M) / (1 + jw*0.2*t_s)^n_M
    ///            -> "이상적 폐루프"의 주파수 응답. tau_M=지연, t_s=정착시간, n_M=차수
    ///   (3.0.18) W(jw) = wW / (jw + wW)
    ///            -> 저역통과 가중 필터. 고주파 노이즈 억제용.
    ///   (2.3.11) F(jw) = M(jw) * (1-M(jw)) * W(jw)
    ///            -> 모델 매칭 필터. VRFT 회귀의 편향을 줄이는 역할.
    ///
    ///   rv = M^{-1}[y]   -> "M대로 작동했다면 레퍼런스는 뭐였을까?" (가상 레퍼런스)
    ///   ev = rv - y       -> 가상 에러
    ///   회귀: uF = rho1*eF + rho2*integral(eF) + rho3*d(eF)/dt
    ///   -> rho1=Kp, rho2=Kp/Ti, rho3=Kp*Td
    ///
    /// ■ C# 클래스 구조:
    ///   SuperScreen{T} = FTD UI 시스템의 "탭 화면" 기본 클래스.
    ///   T = VariableControllerMaster = FTD의 PID 제어기 객체.
    ///   이 클래스를 상속하면 FTD UI 창 안에 탭으로 들어갈 수 있다.
    ///   this._focus = 부모 클래스에서 물려받는 필드, 현재 편집 중인 PID 제어기.
    /// </summary>
    public class VrftTuningTab : SuperScreen<VariableControllerMaster>
    {
        // ── MathNet 편의 팩토리 ──
        // C#에서 System.Numerics.Vector{T}와 MathNet의 Vector{T}가 이름이 겹쳐서
        // "어떤 Vector?"라는 모호성 오류가 남. 여기서 MathNet 것을 명시적으로 선언.
        // MB.Dense(행, 열) -> 행렬 생성,  VB.DenseOfArray(배열) -> 벡터 생성
        private static readonly MatrixBuilder<double> MB = Matrix<double>.Build;
        private static readonly VectorBuilder<double> VB = MathNet.Numerics.LinearAlgebra.Vector<double>.Build;

        // ■ enum = 이름 붙인 정수 상수 모음. Python의 Enum과 동일.
        //   private = 이 클래스 안에서만 사용 가능.

        /// <summary>가진(excitation) 파형 종류</summary>
        private enum WaveType
        {
            Off = 0,        // 가진 없음
            Sine = 1,       // 단일 사인파
            Chirp = 2,      // 주파수 스윕 (시간에 따라 주파수 증가)
            MultiSine = 3   // 여러 주파수 사인파 합성
        }

        /// <summary>자동 튜닝 상태 머신</summary>
        private enum AutoTuneState
        {
            Idle,       // 대기 중
            Recording,  // 데이터 수집 중 (폐루프)
            Computing,  // 수집 끝, VRFT 계산 중
            Done,       // 계산 완료 (결과 있음)
            Failed,     // 실패 (에러 메시지 있음)
            OpenLoop    // 개루프 스텝 응답 수집 중
        }

        /// <summary>축 타입 — 사용자가 각 tab 에서 지정. 피치 고도유지 로직 등에 사용.</summary>
        public enum AxisType
        {
            Unspecified,  // 미지정 (기본값)
            Yaw, Roll, Pitch, Hover, Forward, Strafe
        }

        // ■ sealed class = 상속 불가 클래스. "이 클래스를 더 확장하지 않겠다"는 의미.
        //   private = VrftTuningTab 안에서만 사용.
        //   Settings는 UI 슬라이더와 연결되는 "설정값 묶음".

        /// <summary>VRFT 튜닝에 사용되는 모든 설정값</summary>
        private sealed class Settings
        {
            // ===== 참조모델 M: "폐루프가 이렇게 반응했으면 좋겠다" =====
            public float SettlingTimeTs = 2.0f;     // t_s: 목표 정착시간 (초). 작을수록 빠른 응답 요구
            public int ModelOrderNm = 2;            // n_M: 모델 차수. 높을수록 오버슈트 적지만 느림
            public float ModelDelayTau = 0.0f;      // tau_M: 목표 지연 (초). 자동 추정됨

            // ===== 가중 필터 W: 고주파 노이즈 억제 =====
            public float CutoffHz = 30.0f;          // f_W: 컷오프 주파수 (Hz). 이 위 주파수는 무시

            // ===== 녹화/전처리 =====
            public int MinSamples = 1024;           // 최소 수집 샘플 수 (FFT 해상도에 영향)
            public int DropEdgeSamples = 64;        // FFT 양끝 아티팩트 버릴 개수

            // ===== 포화 처리 =====
            public bool RejectSaturated = true;     // 포화 샘플을 버릴지
            public float SaturationThreshold = 0.98f; // |u| >= 이 값이면 포화로 판정

            // ===== 가진(Excitation): 플랜트를 흔들어서 데이터를 만드는 신호 =====
            public bool ExciteEnabled = true;       // 가진 켤지
            public WaveType ExciteWave = WaveType.Sine; // 가진 파형 종류
            public float ExciteAmp = 0.5f;          // 가진 진폭 (SetPoint에 더해지는 크기)
            public float ExciteFreqHz = 0.6f;       // Sine/MultiSine 기본 주파수 (Hz)
            public float ChirpStartHz = 0.2f;       // Chirp 시작 주파수
            public float ChirpEndHz = 2.0f;         // Chirp 끝 주파수

            // ===== 적응형 진폭: PID가 가진을 다 눌러버릴 때 자동으로 키움 =====
            public bool AdaptiveAmp = true;         // 적응형 켤지
            public float AdaptiveAmpMax = 10.0f;    // 최대 허용 진폭
            public float AdaptiveSnrTarget = 0.15f; // stddev(y)/amp가 이보다 작으면 진폭 증가

            // ===== 축 분리 (Axis Fixture) =====
            public bool FixOtherAxes = true;        // 튜닝 중 다른 축 SP 고정
            public AxisType AxisKind = AxisType.Unspecified;  // 이 탭의 축 타입
            public float PitchAltHoldGain = 0.01f;  // 고도 오차 (m) → 피치 SP 오프셋 스케일
            public float PitchAltHoldClamp = 0.3f;  // 피치 SP 오프셋 최대 크기
        }

        /// <summary>
        /// 현재 녹화 세션의 실시간 상태.
        /// 녹화 시작 시 Clear()로 초기화, 매 틱마다 U/Y에 데이터 추가.
        /// readonly = "리스트 객체 자체는 교체 불가, 안에 요소 추가/삭제는 가능"
        /// </summary>
        private sealed class Session
        {
            public bool Recording;                   // 지금 녹화 중인지
            public double T;                          // 경과 시간 (초)

            public readonly List<double> U = new List<double>();  // 제어 출력 기록
            public readonly List<double> Y = new List<double>();  // 프로세스 변수 기록
            public readonly List<double> R = new List<double>();  // 가진 신호 (외생) — BLA 폐루프 편향 제거용

            // MISO N4SID용: 다른 축의 u 기록 (커플링 분리)
            public readonly List<List<double>> OtherU = new List<List<double>>();

            // ── 블록 관리 ──
            // 포화 샘플을 버리면 시계열에 "구멍"이 생김.
            // 구멍이 있는 데이터를 FFT하면 시간 정합이 깨짐.
            // 해결: 구멍 전후를 "블록"으로 분리, 계산 시 가장 긴 블록만 사용.
            // BlockStarts = [0, 512, 1024] → 블록: [0,512), [512,1024), [1024,끝)
            public readonly List<int> BlockStarts = new List<int> { 0 };
            public bool NeedNewBlock;  // 다음 유효 샘플에서 새 블록 시작 필요

            public int SaturatedCount;
            public int RejectedCount;
            public int ConsecutiveSaturated;         // 현재 연속 포화 틱 수
            public int ConsecutiveSatThreshold = 10; // 이 이상 연속이면 블록 분리
            public double BlockStartT;               // 현재 블록의 시작 시간 (chirp 리셋용)

            // 적응형 진폭 상태
            public double AdaptiveCurrentAmp;    // 현재 실제 적용 진폭
            public double AdaptiveYSum;          // y 합 (평균 계산용)
            public double AdaptiveYSqSum;        // y² 합 (분산 계산용)
            public double AdaptiveUSum;          // u 합 (제어 활동도 측정)
            public double AdaptiveUSqSum;        // u² 합
            public int AdaptiveCount;            // 누적 횟수
            public int AdaptiveCheckInterval = 60; // 몇 샘플마다 체크 (약 1~1.5초)
            public int AdaptiveBoostCount;       // 진폭 증가 횟수
            public double LastU;                 // 마지막 제어 출력 (포화 회피용)
            public double NaturalYStd;           // 가진 전 자연 변동 (y의 std)

            public bool HasResult;
            public double Kp, Ti, Td;
            public double FitRmse;
            public string ActiveMethodName = "";     // 현재 활성 결과 ("PEM" / "VRFT" / ...)
            public string ActiveMethodInfo = "";     // 상세 정보 문자열

            // 대안 결과 (사용자가 스왑 가능). 사용 안 하면 HasAlt=false.
            public bool HasAlt;
            public double AltKp, AltTi, AltTd;
            public string AltMethodName = "";
            public string AltMethodInfo = "";

            public string LastMessage = "";

            public void Clear()
            {
                Recording = false;
                T = 0;
                U.Clear();
                Y.Clear();
                R.Clear();
                OtherU.Clear();
                BlockStarts.Clear();
                BlockStarts.Add(0);
                NeedNewBlock = false;
                SaturatedCount = 0;
                RejectedCount = 0;
                ConsecutiveSaturated = 0;
                BlockStartT = 0;
                AdaptiveCurrentAmp = 0;
                AdaptiveYSum = 0;
                AdaptiveYSqSum = 0;
                AdaptiveUSum = 0;
                AdaptiveUSqSum = 0;
                AdaptiveCount = 0;
                AdaptiveBoostCount = 0;
                LastU = 0;
                NaturalYStd = 0;
                HasResult = false;
                Kp = Ti = Td = FitRmse = 0;
                ActiveMethodName = ActiveMethodInfo = "";
                HasAlt = false;
                AltKp = AltTi = AltTd = 0;
                AltMethodName = AltMethodInfo = "";
                LastMessage = "";
            }
        }

        // ── 인스턴스 필드 ──
        // _s: 설정값 (슬라이더와 연결)
        // _sess: 현재 녹화 세션 상태 (데이터, 결과 등)
        // _autoState: 자동 튜닝 상태 머신
        private readonly Settings _s = new Settings();
        private readonly Session _sess = new Session();
        private AutoTuneState _autoState = AutoTuneState.Idle;
        private OpenLoopCollector _openLoopCollector = null;

        // 다른 축 Controller (MISO N4SID용 커플링 분석)
        private readonly List<VariableControllerMaster> _otherAxes = new List<VariableControllerMaster>();

        /// <summary>다른 축 Controller를 등록 (패치에서 호출)</summary>
        public void SetOtherAxes(params VariableControllerMaster[] axes)
        {
            _otherAxes.Clear();
            foreach (var ax in axes)
            {
                if (ax != null && ax != this._focus)
                    _otherAxes.Add(ax);
            }
        }

        // ── 축 분리 모드 (Axis Fixture) ──
        // 방법 1: 리플렉션으로 _focus 의 부모 객체에서 형제 VariableControllerMaster 자동 발견.
        // 방법 2: 사용자가 각 축 PID UI 열면 자동 등록 (_tabsByAxis).
        // 두 방법 병합 — 리플렉션 성공하면 자동, 실패하면 수동 등록 폴백.
        private static readonly Dictionary<VariableControllerMaster, VrftTuningTab> _tabsByAxis
            = new Dictionary<VariableControllerMaster, VrftTuningTab>();
        private static bool _axisDiscoveryAttempted = false;
        private readonly Dictionary<VariableControllerMaster, float> _frozenOtherSPs
            = new Dictionary<VariableControllerMaster, float>();

        // ── 피치 고도 유지 (Pitch Altitude Hold) ──
        // 비행기형 기체는 피치를 고도 제어에 사용 → 튜닝 중 피치 SP 를 고정하면 고도 드리프트.
        // 해결: Hover 축이 등록되어 있으면 그 PV 를 고도 기준으로 사용, 피치 SP 에 실시간 offset 주입.
        //   pitchOffset = clamp(K_alt · (startAlt - currentAlt), ±clampMax)
        // 가진 0.05~2Hz vs 고도 루프 ~0.01Hz → 대역 분리 → 피치 SISO 데이터 깨끗.
        private VariableControllerMaster _altitudeSourceAxis;  // Hover 로 지정된 축 (PV=고도)
        private VariableControllerMaster _pitchTargetAxis;     // Pitch 로 지정된 축 (SP 받음)
        private bool _altHoldActive;
        private double _altHoldStartAltitude;

        // 가진 적용 시 원래 SetPoint를 백업해두고, 녹화 끝나면 복원하기 위한 변수.
        // SetPointAdjust = FTD에서 PID의 목표값을 외부에서 조절하는 파라미터.
        private bool _hasBaseSetPointAdjust;
        private float _baseSetPointAdjust;

        // 자연 변동 측정: 녹화 전 y를 링버퍼에 모아서 std 계산
        private const int NaturalBufSize = 60; // 약 1.2초 분량
        private readonly double[] _naturalYBuf = new double[NaturalBufSize];
        private int _naturalYIdx = 0;
        private int _naturalYCount = 0;

        /// <summary>
        /// 생성자. FTD가 PID 편집 UI를 열 때 패치에서 호출.
        /// : base(window, focus) = 부모 클래스(SuperScreen) 생성자에 window와 focus를 넘김.
        /// this._focus = focus (부모에서 설정됨) → 이후 this._focus로 PID 제어기에 접근.
        /// </summary>
        public VrftTuningTab(ConsoleWindow window, VariableControllerMaster focus) : base(window, focus)
        {
            // Name = 탭 이름. Content(표시텍스트, 툴팁, 내부ID)
            this.Name = new Content("VRFT Tuning / VRFT 튜닝", new ToolTip("Auto-estimate PID (Kp, Ti, Td) via VRFT.\n---\nVRFT로 PID(Kp, Ti, Td)를 자동 추정합니다.", 220f), "vrft");

            // 정적 registry 에 등록 — 다른 축 튜닝 시 이 축 SP 고정 대상으로 사용
            if (focus != null) _tabsByAxis[focus] = this;
        }

        /// <summary>
        /// FTD UI 시스템이 탭을 그릴 때 호출. UI 요소들을 여기서 생성/배치.
        /// override = 부모 클래스의 같은 이름 메서드를 덮어쓰기.
        /// </summary>
        public override void Build()
        {
            BuildStatus();              // 상태 표시 영역
            BuildSettingsSliders();     // 설정 슬라이더들
            BuildExcitationControls();  // 가진 설정 (파형/진폭/주파수)
            BuildActionButtons();       // 버튼들 (자동튜닝, 녹화, 계산, 적용)
            BuildResult();              // 결과 표시 (Kp, Ti, Td, RMSE)
        }

        // ============================================================
        // FixedUpdate tick — 매 물리 프레임(보통 0.02초=50Hz)마다 호출됨.
        // VariableControllerUiFixedUpdatePatch가 Harmony로 FTD 코드에 끼어들어서
        // 이 메서드를 호출해 줌. 이게 이 모드의 "심장박동".
        //
        // 하는 일:
        // 1) 가진 신호를 SetPoint에 더함
        // 2) u(제어 출력), y(프로세스 변수) 읽어서 저장
        // 3) 적응형 진폭 조절
        // 4) 포화 샘플 처리
        // 5) 자동 튜닝: 충분히 모이면 계산 단계로 전환
        // ============================================================
        public void OnUiFixed()
        {
            try
            {
                if (this._focus == null)
                {
                    _sess.LastMessage = "focus is null / focus가 null입니다.";
                    StopRecording();
                    return;
                }

                // 개루프 스텝 응답 수집 → FOPDT 식별 → IMC-PID 산출
                if (_autoState == AutoTuneState.OpenLoop)
                {
                    if (_openLoopCollector != null && _openLoopCollector.IsDone())
                    {
                        this._focus.DataCollector = null; // PID 복귀

                        try
                        {
                            double olDt = Time.fixedDeltaTime;
                            if (olDt <= 0) olDt = 0.02;

                            int olN = _openLoopCollector.Y.Count;
                            if (olN < 20)
                            {
                                _autoState = AutoTuneState.Failed;
                                _sess.LastMessage = "Open-loop: insufficient data / 개루프: 데이터 부족";
                                return;
                            }

                            double uStep = _openLoopCollector.U.Count > 0 ? _openLoopCollector.U[0] : 0.3;
                            double y0 = _openLoopCollector.Y0;

                            // ── 1. 정상상태 수렴 판별: FOPDT vs IPDT ──
                            // 후반 50%의 y 변화율(기울기)로 판단
                            int halfStart = olN / 2;
                            double sumT = 0, sumY = 0, sumTY = 0, sumTT = 0;
                            for (int i = halfStart; i < olN; i++)
                            {
                                double t = i * olDt;
                                double yi = _openLoopCollector.Y[i];
                                sumT += t;
                                sumY += yi;
                                sumTY += t * yi;
                                sumTT += t * t;
                            }
                            int nHalf = olN - halfStart;
                            double meanT = sumT / nHalf;
                            double meanY = sumY / nHalf;
                            // 후반 기울기 (선형 회귀)
                            double slope = (sumTY - nHalf * meanT * meanY) / Math.Max(1e-12, sumTT - nHalf * meanT * meanT);
                            double yFinal = meanY; // 후반 평균

                            // 적분기 판별: 후반 기울기가 유의미하면 IPDT
                            bool isIntegrator = Math.Abs(slope) > 0.1; // 0.1 단위/초 이상이면 적분기

                            // ── 2. 순수 지연 τ 추정 ──
                            // y가 y0에서 처음 유의미하게 벗어나는 시간 (5% of 1초 후 변화량)
                            double yAt1s = (olN > (int)(1.0 / olDt))
                                ? _openLoopCollector.Y[Math.Min((int)(1.0 / olDt), olN - 1)]
                                : _openLoopCollector.Y[olN - 1];
                            double earlyRange = Math.Abs(yAt1s - y0);
                            double delayThresh = y0 + Math.Sign(yAt1s - y0) * Math.Max(earlyRange * 0.05, 0.01);
                            double tauDelay = 0;
                            for (int i = 0; i < olN; i++)
                            {
                                double yi = _openLoopCollector.Y[i];
                                if ((yAt1s > y0 && yi >= delayThresh) || (yAt1s < y0 && yi <= delayThresh))
                                {
                                    tauDelay = i * olDt;
                                    break;
                                }
                            }
                            tauDelay = Math.Max(0, Math.Min(tauDelay, 0.5));

                            double kp, ti, td;
                            string modelType;

                            if (isIntegrator)
                            {
                                // ── IPDT 모델: P(s) = Kv/s * e^(-τs) ──
                                // Kv = dy/dt / u (후반 기울기 / 입력)
                                double Kv = slope / Math.Max(1e-6, Math.Abs(uStep));

                                // IMC-PID for IPDT:
                                // λ = max(2τ, 0.2)
                                double lambda = Math.Max(2.0 * tauDelay, 0.2);
                                kp = 1.0 / (Math.Abs(Kv) * (2.0 * tauDelay + lambda));
                                ti = 2.0 * (2.0 * tauDelay + lambda);
                                td = tauDelay;

                                modelType = $"IPDT Kv={Kv:0.000}";
                            }
                            else
                            {
                                // ── FOPDT 모델: P(s) = K/(1+τp*s) * e^(-τs) ──
                                double K = (yFinal - y0) / Math.Max(1e-6, Math.Abs(uStep));

                                // 시정수: 63.2% 도달 시간
                                double threshold63 = y0 + (yFinal - y0) * 0.632;
                                double tauP = olN * olDt; // fallback
                                for (int i = 0; i < olN; i++)
                                {
                                    double yi = _openLoopCollector.Y[i];
                                    if ((yFinal > y0 && yi >= threshold63) || (yFinal < y0 && yi <= threshold63))
                                    {
                                        tauP = i * olDt - tauDelay;
                                        break;
                                    }
                                }
                                tauP = Math.Max(olDt, tauP);

                                // IMC-PID for FOPDT:
                                double lambda = Math.Max(tauDelay, 0.1);
                                kp = tauP / (Math.Abs(K) * (tauDelay + lambda));
                                ti = tauP;
                                td = tauDelay / 2.0;

                                modelType = $"FOPDT K={K:0.000} τp={tauP:0.00}";
                            }

                            // 부호 보정
                            kp = Math.Abs(kp);

                            // 클램핑
                            kp = Math.Max(0.001, Math.Min(1.0, kp));
                            ti = Math.Max(0.1, Math.Min(250.0, ti));
                            td = Math.Max(0.0, Math.Min(10.0, td));

                            // PID 적용
                            this._focus.Pid.kP.Us = (float)kp;
                            this._focus.Pid.kI.Us = (float)ti;
                            this._focus.Pid.kD.Us = (float)td;

                            _sess.HasResult = true;
                            _sess.Kp = kp;
                            _sess.Ti = ti;
                            _sess.Td = td;
                            _autoState = AutoTuneState.Done;
                            _sess.LastMessage = $"Step ID ({modelType}) τ={tauDelay:0.00} y0={y0:0.0} slope={slope:0.0} N={olN} → Kp={kp:0.000} Ti={ti:0.0} Td={td:0.00}";
                            _openLoopCollector = null; // cleanup
                        }
                        catch (Exception e)
                        {
                            _autoState = AutoTuneState.Failed;
                            _sess.LastMessage = "Open-loop failed: " + e.Message;
                            _openLoopCollector = null; // cleanup on failure too
                        }
                    }
                    else
                    {
                        int olCount = _openLoopCollector != null ? _openLoopCollector.U.Count : 0;
                        _sess.LastMessage = $"Step response: {olCount} samples / 스텝 응답 수집 중";
                    }
                    return;
                }

                // 자동 튜닝 Computing 상태 처리 (녹화 중지 후 다음 틱)
                if (_autoState == AutoTuneState.Computing)
                {
                    try { AutoTuneCompute(); }
                    catch (Exception e)
                    {
                        _autoState = AutoTuneState.Failed;
                        _sess.LastMessage = "Auto-tune failed / 자동 튜닝 실패: " + e.Message;
                    }
                    return;
                }

                if (!_sess.Recording)
                {
                    RestoreSetPointAdjustIfNeeded();

                    // 녹화 전 자연 변동 측정: y를 링버퍼에 수집
                    IVariableController cIdle = this._focus.GetCurrentController();
                    if (cIdle != null)
                    {
                        _naturalYBuf[_naturalYIdx % NaturalBufSize] = cIdle.LastProcessVariable;
                        _naturalYIdx++;
                        if (_naturalYCount < NaturalBufSize) _naturalYCount++;
                    }
                    return;
                }

                double dt = Time.fixedDeltaTime;
                if (dt <= 0) dt = 0.02;

                ApplyExcitation((float)dt);
                ApplyOtherAxesFixture();  // 다른 축 SP 재적용 (매 틱) — 자세/고도 유지

                IVariableController c = this._focus.GetCurrentController();
                if (c == null) return;

                // u: 제어 출력(컨트롤 변수), y: 프로세스 변수, sp: 목표값
                double u = c.LastControlVariable;
                double y = c.LastProcessVariable;
                _sess.LastU = u;

                // ── 적응형 진폭: 정보량 부족 시 진폭 증가 ──
                // 정보량 판단: y 변동 (yStd/amp) AND u 제어 활동 (uStd) 둘 다 고려.
                //   기존 yStd/amp 만 보면 강한 PID / 고주파 컷오프 상황에서 u가 활발해도 boost 됨
                //   → 불필요한 포화 유발. u 가 이미 움직이면 N4SID/PEM 식별에 정보 충분.
                if (_s.AdaptiveAmp && _s.ExciteEnabled && _autoState == AutoTuneState.Recording
                    && _sess.ConsecutiveSaturated < _sess.ConsecutiveSatThreshold)
                {
                    _sess.AdaptiveYSum += y;
                    _sess.AdaptiveYSqSum += y * y;
                    _sess.AdaptiveUSum += u;
                    _sess.AdaptiveUSqSum += u * u;
                    _sess.AdaptiveCount++;

                    if (_sess.AdaptiveCount >= _sess.AdaptiveCheckInterval)
                    {
                        double yMean = _sess.AdaptiveYSum / _sess.AdaptiveCount;
                        double yVar = (_sess.AdaptiveYSqSum / _sess.AdaptiveCount) - yMean * yMean;
                        double yStd = yVar > 0 ? Math.Sqrt(yVar) : 0;
                        double uMean = _sess.AdaptiveUSum / _sess.AdaptiveCount;
                        double uVar = (_sess.AdaptiveUSqSum / _sess.AdaptiveCount) - uMean * uMean;
                        double uStd = uVar > 0 ? Math.Sqrt(uVar) : 0;
                        double amp = Math.Max(0.01, _sess.AdaptiveCurrentAmp);

                        double yRatio = yStd / amp;          // 폐루프 민감도 근사
                        const double U_INFO_THRESHOLD = 0.1; // u 변동이 이 이상이면 정보 충분
                        bool yLow = yRatio < _s.AdaptiveSnrTarget;
                        bool uLow = uStd < U_INFO_THRESHOLD;

                        if (yLow && uLow && amp < _s.AdaptiveAmpMax)
                        {
                            double newAmp = Math.Min(amp * 2.0, _s.AdaptiveAmpMax);
                            _sess.AdaptiveCurrentAmp = newAmp;
                            _s.ExciteAmp = (float)newAmp;
                            _sess.AdaptiveBoostCount++;

                            // 진폭 변경 후 새 블록 시작 → 이전 약한 데이터와 분리
                            if (_sess.U.Count > 0)
                            {
                                _sess.BlockStarts.Add(_sess.U.Count);
                                _sess.BlockStartT = _sess.T; // chirp를 새 블록에서 처음부터 다시
                            }

                            _sess.LastMessage = $"Adaptive: amp {amp:0.00}→{newAmp:0.00} / 적응: 진폭 {amp:0.00}→{newAmp:0.00} (yStd/amp={yRatio:0.00}, uStd={uStd:0.00} 둘 다 낮음)";
                        }
                        else if (yLow && !uLow)
                        {
                            _sess.LastMessage = $"Info OK (u active, uStd={uStd:0.00}) / 정보 충분: u 활발 (yStd/amp={yRatio:0.00})";
                        }

                        _sess.AdaptiveYSum = 0;
                        _sess.AdaptiveYSqSum = 0;
                        _sess.AdaptiveUSum = 0;
                        _sess.AdaptiveUSqSum = 0;
                        _sess.AdaptiveCount = 0;
                    }
                }

                bool saturated = Math.Abs(u) >= _s.SaturationThreshold;
                if (saturated)
                {
                    _sess.SaturatedCount++;
                    _sess.ConsecutiveSaturated++;
                }
                else
                {
                    // 연속 포화가 임계값 이상이었으면 → 블록 분리 (긴 포화 구간 끝)
                    if (_sess.ConsecutiveSaturated >= _sess.ConsecutiveSatThreshold
                        && _sess.U.Count > 0)
                    {
                        _sess.BlockStarts.Add(_sess.U.Count);
                        _sess.BlockStartT = _sess.T; // chirp를 새 블록에서 처음부터 다시
                    }
                    _sess.ConsecutiveSaturated = 0;
                }

                // 짧은 포화(< threshold틱): 데이터 유지 (u가 클리핑됐어도 실제 입력)
                // 긴 연속 포화 중: 데이터 버림 (비선형 영역)
                if (_s.RejectSaturated && _sess.ConsecutiveSaturated >= _sess.ConsecutiveSatThreshold)
                {
                    _sess.RejectedCount++;
                    _sess.T += dt;
                    return;
                }

                _sess.U.Add(u);
                _sess.Y.Add(y);
                _sess.R.Add(_lastExciteValue); // 외생 가진 신호 (BLA 무편향 식별용)

                // 다른 축 u도 기록 (MISO N4SID용)
                while (_sess.OtherU.Count < _otherAxes.Count)
                    _sess.OtherU.Add(new List<double>());
                for (int ai = 0; ai < _otherAxes.Count; ai++)
                {
                    IVariableController oc = _otherAxes[ai].GetCurrentController();
                    _sess.OtherU[ai].Add(oc != null ? oc.LastControlVariable : 0);
                }

                _sess.T += dt;

                if (_sess.U.Count % 240 == 0)
                {
                    var (_, bestLen) = PickLongestBlock(_sess.BlockStarts, _sess.U.Count);
                    _sess.LastMessage = $"Collecting... best block {bestLen}/{_s.MinSamples} (total {_sess.U.Count}, boost {_sess.AdaptiveBoostCount}) / 수집중... 최장블록 {bestLen}/{_s.MinSamples}";
                }

                // 자동 튜닝: 최장 블록이 MinSamples에 도달하면 중지
                if (_autoState == AutoTuneState.Recording)
                {
                    var (_, bestLen) = PickLongestBlock(_sess.BlockStarts, _sess.U.Count);
                    if (bestLen >= _s.MinSamples)
                    {
                        StopRecording();
                        _autoState = AutoTuneState.Computing;
                        _sess.LastMessage = "Auto-tune: analyzing... / 자동 튜닝: 데이터 분석 중...";
                    }
                    // 블록 분리가 많으면 경고만 표시 (Kp는 건드리지 않음)
                    else if (_sess.BlockStarts.Count >= 5)
                    {
                        _sess.LastMessage = $"Many block splits ({_sess.BlockStarts.Count}). Data may be noisy. / 블록 분리 다수 ({_sess.BlockStarts.Count}). 데이터 품질 주의.";
                    }
                }
            }
            catch (Exception e)
            {
                _sess.LastMessage = "OnUiFixed error / 오류: " + e.Message;
                StopRecording();
            }
        }

        // ============================================================
        // UI builders
        // ============================================================

        private void BuildStatus()
        {
            ScreenSegmentStandard seg = base.CreateStandardSegment(InsertPosition.OnCursor);
            seg.BackgroundStyleWhereApplicable = ConsoleStyles.Instance.Styles.Segments.OptionalSegmentDarkBackgroundWithHeader.Style;
            seg.NameWhereApplicable = "Status / 상태";
            seg.SpaceAbove = 10f;
            seg.SpaceBelow = 10f;

            seg.AddInterpretter(new SubjectiveDisplay<VariableControllerMaster>(
                this._focus,
                M.m<VariableControllerMaster>(_ =>
                {
                    string rec;
                    if (_autoState == AutoTuneState.Computing)
                        rec = "Computing / 계산 중";
                    else if (_sess.Recording)
                        rec = "Recording / 녹화중";
                    else if (_autoState == AutoTuneState.OpenLoop)
                        rec = "Step ID / 스텝 식별 중";
                    else if (_autoState == AutoTuneState.Done)
                        rec = "Done / 완료";
                    else if (_autoState == AutoTuneState.Failed)
                        rec = "Failed / 실패";
                    else
                        rec = "Idle / 대기";
                    double dt = Time.fixedDeltaTime;

                    int progressCount = 0;
                    if (_openLoopCollector != null)
                        progressCount = _openLoopCollector.U.Count;
                    else
                    {
                        int lastBlkStart = _sess.BlockStarts.Count > 0
                            ? _sess.BlockStarts[_sess.BlockStarts.Count - 1] : 0;
                        progressCount = _sess.U.Count - lastBlkStart;
                    }

                    return
                        $"Status: {rec}\n" +
                        $"Samples: {progressCount} / {_s.MinSamples}  (blocks: {_sess.BlockStarts.Count})\n" +
                        $"Saturated: {_sess.SaturatedCount}  |  Rejected: {_sess.RejectedCount}\n" +
                        $"FixedDeltaTime: {dt:0.000}s\n" +
                        $"Msg: {_sess.LastMessage}";
                }),
                M.m<VariableControllerMaster>(new ToolTip(
                    "Shows current VRFT recording status, sample count, and saturated/rejected samples.\nSamples accumulate every FixedUpdate during recording.\n---\n" +
                    "현재 VRFT 기록 상태와 샘플 수, 포화/제외된 샘플 수를 표시합니다.\n" +
                    "샘플은 녹화 중 FixedUpdate마다 누적됩니다.",
                    260f
                ))
            ));
        }

        private void BuildSettingsSliders()
        {
            ScreenSegmentTable table = base.CreateTableSegment(1, 10);
            table.BackgroundStyleWhereApplicable = ConsoleStyles.Instance.Styles.Segments.OptionalSegmentDarkBackgroundWithHeader.Style;
            table.NameWhereApplicable = "VRFT Settings / VRFT 설정";
            table.SpaceAbove = 10f;
            table.SpaceBelow = 10f;
            table.SqueezeTable = false;

            // t_s
            table.AddInterpretter(MakeSliderFloat(
                "Settling time t_s (s) / 정착시간 t_s (초)",
                "Target settling time. Smaller = faster response.\nAuto-tuning estimates this automatically.\n---\n목표 정착시간. 작을수록 빠른 응답.\n자동 튜닝 시 자동 추정됩니다.",
                () => _s.SettlingTimeTs,
                f => _s.SettlingTimeTs = Clamp(f, 0.2f, 60f),
                0.2f, 60f, 0.1f, "0.0", "Ts"
            ));

            // tau_M
            table.AddInterpretter(MakeSliderFloat(
                "Delay τ_M (s) / 지연 τ_M (초)",
                "Plant delay (dead-time). 0 = no delay.\nAuto-tuning estimates this automatically.\n---\n플랜트 지연. 0이면 지연 없음.\n자동 튜닝 시 자동 추정됩니다.",
                () => _s.ModelDelayTau,
                f => _s.ModelDelayTau = Clamp(f, 0f, 5f),
                0f, 5f, 0.01f, "0.00", "tau"
            ));

            // min samples
            table.AddInterpretter(MakeSliderInt(
                "Min samples / 최소 샘플 수",
                "Minimum samples for data collection.\nMore samples = better accuracy but longer wait.\n---\n데이터 수집 최소 샘플 수.\n많을수록 정확하지만 대기 시간이 길어집니다.",
                () => _s.MinSamples,
                v => _s.MinSamples = ClampInt(v, 256, 200000),
                256, 32768, 256, "0", "N"
            ));
        }

        private void BuildExcitationControls()
        {
            ScreenSegmentStandard seg = base.CreateStandardSegment(InsertPosition.OnCursor);
            seg.BackgroundStyleWhereApplicable = ConsoleStyles.Instance.Styles.Segments.OptionalSegmentDarkBackgroundWithHeader.Style;
            seg.NameWhereApplicable = "Excitation / 자극";
            seg.SpaceAbove = 10f;
            seg.SpaceBelow = 10f;

            seg.AddInterpretter(MakeToggle(
                "Enable excitation / 자극 사용",
                "Adds excitation signal to SetPointAdjust during recording.\nAuto-tuning configures this automatically.\n---\n녹화 중 SetPointAdjust에 가진 신호를 더합니다.\n자동 튜닝 시 자동 설정됩니다.",
                () => _s.ExciteEnabled,
                b => _s.ExciteEnabled = b,
                "excite"
            ));

            // Axis type 선택 (cycle 버튼)
            seg.AddInterpretter(MakeCycleButton(
                "Axis type / 축 타입",
                "Mark this tab's axis so cross-axis features work correctly.\n" +
                "Hover axis's PV = altitude (used for pitch altitude-hold).\n" +
                "Pitch axis receives altitude-hold offset.\n" +
                "Open each axis's PID UI once and set its type.\n---\n" +
                "이 탭의 축 타입 지정. 축간 기능 (피치 고도유지) 에 필요.\n" +
                "Hover 축의 PV = 고도, Pitch 축 SP 에 고도 보정 주입.\n" +
                "튜닝 전 각 축 PID UI 열고 타입 설정.",
                () => _s.AxisKind.ToString(),
                () =>
                {
                    // 순환: Unspecified → Yaw → Roll → Pitch → Hover → Forward → Strafe → Unspecified
                    _s.AxisKind = (AxisType)(((int)_s.AxisKind + 1) % Enum.GetValues(typeof(AxisType)).Length);
                },
                "axistype"
            ));

            seg.AddInterpretter(MakeToggle(
                "Fix other axes / 다른 축 고정",
                "During tuning, other axes' SetPoints are frozen at captured values\n" +
                "so existing PIDs hold attitude/altitude. Open each axis's PID UI\n" +
                "once before tuning to register it.\n" +
                "If Hover + Pitch axes both tagged, Pitch SP receives altitude-hold\n" +
                "offset via Hover's PV (for airplane-style altitude control).\n---\n" +
                "튜닝 중 다른 축 SP를 캡처 값에 고정 → 기존 PID가 자세/고도 유지.\n" +
                "튜닝 전 각 축 PID UI를 한 번씩 열어 등록 필요.\n" +
                "Hover + Pitch 모두 태그되면 Hover PV로 피치 SP에 고도 보정 주입.",
                () => _s.FixOtherAxes,
                b => _s.FixOtherAxes = b,
                "fixaxes"
            ));

            ScreenSegmentTable excTable = base.CreateTableSegment(1, 3);
            excTable.SqueezeTable = false;

            excTable.AddInterpretter(MakeSliderFloat(
                "Amplitude A / 자극 진폭 A",
                "Excitation amplitude. Auto-tuning sets this automatically.\n---\n자극 진폭. 자동 튜닝 시 자동 설정됩니다.",
                () => _s.ExciteAmp,
                f => _s.ExciteAmp = Clamp(f, 0f, 10f),
                0f, 10f, 0.05f, "0.00", "A"
            ));
        }

        private void BuildActionButtons()
        {
            ScreenSegmentStandardHorizontal seg = base.CreateStandardHorizontalSegment();
            seg.BackgroundStyleWhereApplicable = ConsoleStyles.Instance.Styles.Segments.OptionalSegmentDarkBackgroundWithHeader.Style;
            seg.NameWhereApplicable = "Actions / 동작";
            seg.SpaceAbove = 10f;
            seg.SpaceBelow = 10f;

            seg.AddInterpretter(new SubjectiveButton<VariableControllerMaster>(
                this._focus,
                M.m<VariableControllerMaster>(_ => _autoState == AutoTuneState.OpenLoop ? "Step ID... / 스텝 식별 중..." : "Step ID / 스텝 식별"),
                M.m<VariableControllerMaster>(new ToolTip(
                    "Open-loop step response → FOPDT model → IMC-PID.\n~3s open-loop test. Use when PID is unknown or bad.\nVehicle briefly uncontrolled!\n---\n개루프 스텝 응답 → FOPDT 모델 → IMC-PID.\n~3초 개루프 테스트. PID를 모르거나 나쁠 때 사용.\n기체가 잠시 무제어!", 260f)),
                null,
                _ => OpenLoopTuneNow()
            ));

            seg.AddInterpretter(new SubjectiveButton<VariableControllerMaster>(
                this._focus,
                M.m<VariableControllerMaster>(_ => _autoState == AutoTuneState.Recording ? "Auto-tuning... / 자동 튜닝 중..." : "Auto Tune / 자동 튜닝"),
                M.m<VariableControllerMaster>(new ToolTip(
                    "Closed-loop VRFT: excitation → record → compute.\nRequires a reasonably stable PID first (use Step ID if needed).\n---\n폐루프 VRFT: 가진 → 녹화 → 계산.\n먼저 적당한 PID가 필요 (필요 시 스텝 식별 사용).", 260f)),
                null,
                _ => AutoTuneNow()
            ));

            seg.AddInterpretter(MakeButton(
                "Record start/stop / 녹화 시작/중지",
                "Start/stop sample collection.\nDuring recording, u (output) and y (process variable) are saved every FixedUpdate.\n---\n샘플 수집을 시작/중지합니다.\n" +
                "녹화 중에는 FixedUpdate마다 u(출력), y(과정변수) 샘플을 저장합니다.",
                _ =>
                {
                    if (_sess.Recording) StopRecording();
                    else StartRecording();
                }
            ));

            seg.AddInterpretter(MakeButton(
                "Reset / 초기화",
                "Clear all saved samples and results.\n---\n저장된 샘플/결과를 모두 지웁니다.",
                _ =>
                {
                    RestoreSetPointAdjustIfNeeded();
                    _sess.Clear();
                    _autoState = AutoTuneState.Idle;
                    _sess.LastMessage = "Reset complete / 초기화 완료";
                }
            ));

            seg.AddInterpretter(MakeButton(
                "Compute (VRFT) / 계산(VRFT)",
                "Compute VRFT per paper structure.\nM,W,F → r_v,e_v → uF,eF → least-squares Kp/Ti/Td estimation.\n---\n논문 구조대로 VRFT를 계산합니다.\n" +
                "M,W,F 구성 → r_v,e_v → uF,eF → 최소자승으로 Kp/Ti/Td 추정",
                _ => ComputeNow()
            ));

            seg.AddInterpretter(MakeButton(
                "Apply / 적용",
                "Apply Kp/Ti/Td to PID. (Kp: 0.001, Ti/Td: 0.1 step)\n---\nKp/Ti/Td를 PID에 적용. (Kp: 0.001, Ti/Td: 0.1 단위)",
                _ => ApplyToPid()
            ));

            seg.AddInterpretter(MakeButton(
                "Swap method / 방법 전환",
                "Swap active result with alternative (PEM ↔ VRFT).\nAuto-selected based on PEM innovation RMS; override here.\n---\n주 결과와 대안 결과를 교체 (PEM ↔ VRFT).\n자동 선택을 수동으로 뒤집을 때 사용.",
                _ => SwapPidMethod()
            ));
        }

        private void BuildResult()
        {
            ScreenSegmentStandard seg = base.CreateStandardSegment(InsertPosition.OnCursor);
            seg.BackgroundStyleWhereApplicable = ConsoleStyles.Instance.Styles.Segments.OptionalSegmentDarkBackgroundWithHeader.Style;
            seg.NameWhereApplicable = "Result / 결과";
            seg.SpaceAbove = 10f;
            seg.SpaceBelow = 10f;

            seg.AddInterpretter(new SubjectiveDisplay<VariableControllerMaster>(
                this._focus,
                M.m<VariableControllerMaster>(_ =>
                {
                    if (!_sess.HasResult)
                        return "No result yet. Press Compute. / 아직 결과가 없습니다.";

                    string methodLine = string.IsNullOrEmpty(_sess.ActiveMethodName)
                        ? ""
                        : $"Active: {_sess.ActiveMethodName}\n{_sess.ActiveMethodInfo}\n\n";
                    string altLine = _sess.HasAlt
                        ? $"\n\n── Alternative ({_sess.AltMethodName}) ──\n" +
                          $"Kp = {_sess.AltKp:0.0000},  Ti = {_sess.AltTi:0.00} s,  Td = {_sess.AltTd:0.0000} s\n" +
                          $"({_sess.AltMethodInfo})\n" +
                          $"→ 'Swap method' 버튼으로 전환 가능"
                        : "";

                    string result = methodLine +
                        $"── Single PID ──\n" +
                        $"Kp = {_sess.Kp:0.0000}\n" +
                        $"Ti = {_sess.Ti:0.00} s\n" +
                        $"Td = {_sess.Td:0.0000} s\n" +
                        $"Fit (RMSE) = {_sess.FitRmse:0.0000}" +
                        altLine;

                    // ── PI(외부) × PD(내부) 분해 ──
                    // Ti_o + Td_i = Ti,  Ti_o * Td_i = Ti * Td
                    // 판별식: Ti² - 4*Ti*Td >= 0 이어야 실수 해 존재
                    double Ti = _sess.Ti;
                    double Td = _sess.Td;
                    double Kp = _sess.Kp;
                    double disc = Ti * Ti - 4.0 * Ti * Td;

                    if (Kp > 1e-6 && Ti > 0.1 && Td > 1e-6 && disc >= 0)
                    {
                        double sqrtDisc = Math.Sqrt(disc);
                        double Ti_o = (Ti + sqrtDisc) / 2.0;  // 외부 (느린 쪽)
                        double Td_i = (Ti - sqrtDisc) / 2.0;  // 내부 (빠른 쪽)

                        if (Ti_o > 1e-6 && Td_i > 1e-6)
                        {
                            double alpha = Ti_o / Td_i;        // 대역폭 비율
                            double product = Kp * Ti_o / Ti;   // Kp_o * Kp_i
                            double sqrtAlpha = Math.Sqrt(alpha);
                            double Kp_o = Math.Sqrt(product / sqrtAlpha);
                            double Kp_i = product / Kp_o;

                            result += $"\n\n── Dual PID (PI×PD, α={alpha:0.0}) ──\n" +
                                      $"Outer PI:  Kp={Kp_o:0.000},  Ti={Ti_o:0.0} s\n" +
                                      $"Inner PD:  Kp={Kp_i:0.000},  Td={Td_i:0.00} s";
                        }
                    }
                    else if (Kp > 1e-6 && Td > 1e-6)
                    {
                        result += $"\n\n── Dual PID: decomposition not possible (Ti < 4·Td required) ──";
                    }

                    return result;
                }),
                M.m<VariableControllerMaster>(new ToolTip(
                    "PID parameters estimated by VRFT regression.\n" +
                    "RMSE = error between filtered output (uF) and regression model. Lower is better.\n" +
                    "Dual PID: PI(outer)×PD(inner) decomposition equivalent to single PID.\n" +
                    "α = inner/outer bandwidth ratio. Larger means inner is faster.\n---\n" +
                    "VRFT 회귀로 추정된 PID 파라미터입니다.\n" +
                    "RMSE는 uF(필터된 출력)와 회귀모델 출력의 오차 크기입니다. 작을수록 좋습니다.\n\n" +
                    "이중 PID: 단일 PID와 동치인 PI(외부)×PD(내부) 분해입니다.\n" +
                    "α = 내부/외부 대역폭 비율. 클수록 내부가 외부보다 빠릅니다.\n" +
                    "중간에 속도 클램프를 넣으면 캐스케이드 제어에 사용 가능합니다.",
                    260f
                ))
            ));
        }

        // ============================================================
        // Open-Loop Tune
        // ============================================================

        private void OpenLoopTuneNow()
        {
            if (_autoState != AutoTuneState.Idle && _autoState != AutoTuneState.Done && _autoState != AutoTuneState.Failed)
            {
                _sess.LastMessage = "Tuning already in progress / 튜닝 이미 진행 중";
                return;
            }

            // 자연 변동 기반 진폭
            double natStd = 0;
            if (_naturalYCount >= 10)
            {
                double sum = 0;
                double sqSum = 0;
                int n = Math.Min(_naturalYCount, NaturalBufSize);
                for (int i = 0; i < n; i++)
                    sum += _naturalYBuf[i];
                double mean = sum / n;
                for (int i = 0; i < n; i++)
                {
                    double d = _naturalYBuf[i] - mean;
                    sqSum += d * d;
                }
                natStd = Math.Sqrt(sqSum / Math.Max(1, n - 1));
            }

            // 스텝 진폭: 0.1~0.3 (개루프라 작게, 포화 방지)
            double amp = Math.Clamp(Math.Max(0.02, natStd * 1.0), 0.02, 0.05);

            // 최대 10초, y가 정상상태에 도달하면 조기 종료
            _openLoopCollector = new OpenLoopCollector(amp, 10.0);

            // DataCollector 설정 → PID 우회
            this._focus.DataCollector = _openLoopCollector;

            _sess.Clear();
            _autoState = AutoTuneState.OpenLoop;
            _sess.LastMessage = $"Step response started (amp={amp:0.00}, max 10s) / 스텝 응답 시작";
        }

        // ============================================================
        // Recording control
        // ============================================================

        private void StartRecording()
        {
            _sess.Clear();
            _sess.Recording = true;
            _sess.LastMessage = "Recording started / 녹화 시작";

            CaptureSetPointAdjustBase();
            CaptureOtherAxesFixture();
        }

        private void StopRecording()
        {
            _sess.Recording = false;
            RestoreSetPointAdjustIfNeeded();
            ReleaseOtherAxesFixture();

            // DataCollector 및 컬렉터 정리 (안전)
            try { if (this._focus != null) this._focus.DataCollector = null; } catch { }
            _openLoopCollector = null;

            if (_autoState == AutoTuneState.Recording)
                _autoState = AutoTuneState.Idle;
            _sess.LastMessage = "Recording stopped / 녹화 중지";
        }

        // ============================================================
        // Axis Fixture — 다른 축 SP 고정 (기존 PID 가 alt/attitude 유지하게)
        // ============================================================

        /// <summary>
        /// 튜닝 시작 시 호출. 현재 등록된 모든 다른 축의 SP 를 캡처.
        /// Recording 중 매 틱 ApplyOtherAxesFixture() 가 이 값으로 재적용.
        /// 동시에 피치 고도 유지용 Hover/Pitch 축 식별 + 시작 고도 캡처.
        /// </summary>
        private void CaptureOtherAxesFixture()
        {
            _frozenOtherSPs.Clear();
            _altitudeSourceAxis = null;
            _pitchTargetAxis = null;
            _altHoldActive = false;

            if (!_s.FixOtherAxes) return;

            // 리플렉션으로 형제 축 자동 발견 시도 (1회만)
            DiscoverSiblingAxes();

            // 등록된 축 중에서 Hover / Pitch 식별 (AxisKind 로)
            foreach (var kv in _tabsByAxis)
            {
                VariableControllerMaster axis = kv.Key;
                if (axis == null) continue;
                try
                {
                    var axisTab = kv.Value;
                    if (axisTab != null && axisTab._s != null)
                    {
                        if (axisTab._s.AxisKind == AxisType.Hover) _altitudeSourceAxis = axis;
                        else if (axisTab._s.AxisKind == AxisType.Pitch) _pitchTargetAxis = axis;
                    }
                }
                catch { }

                // 다른 축 SP 캡처 (현재 축 제외)
                if (axis == this._focus) continue;
                try { _frozenOtherSPs[axis] = axis.SetPointAdjust.Us; }
                catch { }
            }

            // 고도 유지 활성 조건: Hover 축 + Pitch 축 모두 등록됨 + Hover PV 읽기 성공
            if (_altitudeSourceAxis != null && _pitchTargetAxis != null)
            {
                try
                {
                    var hoverCtrl = _altitudeSourceAxis.GetCurrentController();
                    if (hoverCtrl != null)
                    {
                        _altHoldStartAltitude = hoverCtrl.LastProcessVariable;
                        _altHoldActive = true;
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 매 틱 호출.
        ///   1. 다른 축 (피치 제외) SP 를 freeze 값으로 재적용
        ///   2. 피치 축: 고도 유지 offset 계산 후 SP 주입
        ///      - 피치가 튜닝 대상이면 excitation + offset
        ///      - 피치가 대상 아니면 offset 만
        /// </summary>
        private void ApplyOtherAxesFixture()
        {
            // 1. 일반 freeze (피치는 제외 — 아래에서 특수 처리)
            foreach (var kv in _frozenOtherSPs)
            {
                if (_altHoldActive && kv.Key == _pitchTargetAxis) continue;  // 피치는 별도
                try { kv.Key.SetPointAdjust.Us = kv.Value; }
                catch { }
            }

            // 2. 피치 고도 유지 — Hover PV 로 고도 오차 → 피치 SP offset
            if (!_altHoldActive || _pitchTargetAxis == null || _altitudeSourceAxis == null) return;
            try
            {
                var hoverCtrl = _altitudeSourceAxis.GetCurrentController();
                if (hoverCtrl == null) return;
                double currentAlt = hoverCtrl.LastProcessVariable;
                double altErr = _altHoldStartAltitude - currentAlt;
                double clampMax = _s.PitchAltHoldClamp;
                double offset = Math.Max(-clampMax, Math.Min(clampMax, _s.PitchAltHoldGain * altErr));

                if (_pitchTargetAxis == this._focus)
                {
                    // 피치가 튜닝 대상: excitation 위에 offset 더함
                    // ApplyExcitation 가 이미 SP = base + excitation 설정. offset 을 SP 에 더함.
                    _pitchTargetAxis.SetPointAdjust.Us += (float)offset;
                }
                else
                {
                    // 피치 고정 (다른 축 튜닝 중): freeze 값 대신 offset 직접 설정
                    _pitchTargetAxis.SetPointAdjust.Us = (float)offset;
                }
            }
            catch { }
        }

        /// <summary>튜닝 종료 시. freeze 해제 — 다른 축 SP 가 다시 AI 에 의해 제어됨.</summary>
        private void ReleaseOtherAxesFixture()
        {
            _frozenOtherSPs.Clear();
            _altHoldActive = false;
            _altitudeSourceAxis = null;
            _pitchTargetAxis = null;
        }

        // ============================================================
        // 축 자동 발견 (리플렉션) — _focus 의 부모에서 형제 VCM 열거
        // ============================================================

        /// <summary>
        /// _focus 로부터 부모 객체를 리플렉션으로 탐색, 형제 VariableControllerMaster 발견.
        /// 성공하면 _tabsByAxis 에 자동 등록 (tab=null — SP 접근만 가능, UI 설정 없음).
        /// 실패하면 기존 수동 등록 방식으로 폴백.
        /// </summary>
        private void DiscoverSiblingAxes()
        {
            if (_axisDiscoveryAttempted) return;
            _axisDiscoveryAttempted = true;
            if (_focus == null) return;

            try
            {
                var siblings = FindSiblingControllers(_focus);
                int added = 0;
                foreach (var vcm in siblings)
                {
                    if (vcm == null || vcm == _focus) continue;
                    if (!_tabsByAxis.ContainsKey(vcm))
                    {
                        _tabsByAxis[vcm] = null;  // tab 없음 (자동발견), SP 접근만 가능
                        added++;
                    }
                }
                if (added > 0)
                    _sess.LastMessage = $"Auto-discovered {added} sibling axes / {added}개 형제 축 자동 발견";
            }
            catch { }
        }

        /// <summary>
        /// VCM 의 부모 체인을 리플렉션으로 탐색하여 형제 VCM 목록 반환.
        /// 전략: (1) 필드에서 부모 찾기 (2) 부모의 필드/프로퍼티에서 VCM 컬렉션 찾기.
        /// </summary>
        private static List<VariableControllerMaster> FindSiblingControllers(VariableControllerMaster focus)
        {
            var result = new List<VariableControllerMaster>();
            Type vcmType = focus.GetType();
            var allFields = new List<System.Reflection.FieldInfo>();
            var allProps = new List<System.Reflection.PropertyInfo>();
            const System.Reflection.BindingFlags bf =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public;

            // ── 전략 1: focus 자신의 필드에서 부모 객체 탐색 ──
            for (Type t = vcmType; t != null && t != typeof(object); t = t.BaseType)
            {
                allFields.AddRange(t.GetFields(bf | System.Reflection.BindingFlags.DeclaredOnly));
                allProps.AddRange(t.GetProperties(bf | System.Reflection.BindingFlags.DeclaredOnly));
            }

            // 부모 후보: 필드/프로퍼티 중 VCM 이 아니고, null 아닌 참조 타입
            foreach (var field in allFields)
            {
                if (field.FieldType.IsValueType) continue;
                if (field.FieldType == typeof(string)) continue;
                if (typeof(VariableControllerMaster).IsAssignableFrom(field.FieldType)) continue;

                object parent = null;
                try { parent = field.GetValue(focus); } catch { continue; }
                if (parent == null) continue;

                // ── 전략 2: 부모에서 VCM 컬렉션 탐색 ──
                var found = ExtractVcmsFromObject(parent);
                if (found.Count >= 2) // 최소 2개 (self + 형제)
                {
                    result.AddRange(found);
                    return result; // 첫 성공한 경로 사용
                }
            }

            // 프로퍼티도 시도
            foreach (var prop in allProps)
            {
                if (!prop.CanRead) continue;
                if (prop.PropertyType.IsValueType || prop.PropertyType == typeof(string)) continue;

                object parent = null;
                try { parent = prop.GetValue(focus); } catch { continue; }
                if (parent == null) continue;

                var found = ExtractVcmsFromObject(parent);
                if (found.Count >= 2)
                {
                    result.AddRange(found);
                    return result;
                }
            }

            return result;
        }

        /// <summary>객체의 필드/프로퍼티에서 VCM 인스턴스를 모두 추출.</summary>
        private static List<VariableControllerMaster> ExtractVcmsFromObject(object obj)
        {
            var result = new List<VariableControllerMaster>();
            if (obj == null) return result;
            Type objType = obj.GetType();
            const System.Reflection.BindingFlags bf =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public;

            // 직접 VCM 필드
            foreach (var f in objType.GetFields(bf))
            {
                try
                {
                    if (typeof(VariableControllerMaster).IsAssignableFrom(f.FieldType))
                    {
                        var vcm = f.GetValue(obj) as VariableControllerMaster;
                        if (vcm != null) result.Add(vcm);
                    }
                    // VCM 배열
                    else if (f.FieldType.IsArray &&
                             typeof(VariableControllerMaster).IsAssignableFrom(f.FieldType.GetElementType()))
                    {
                        var arr = f.GetValue(obj) as Array;
                        if (arr != null)
                            foreach (var item in arr)
                            {
                                var vcm = item as VariableControllerMaster;
                                if (vcm != null) result.Add(vcm);
                            }
                    }
                    // IEnumerable<VCM> (List, etc.)
                    else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(f.FieldType)
                             && !f.FieldType.IsValueType && f.FieldType != typeof(string))
                    {
                        var enumerable = f.GetValue(obj) as System.Collections.IEnumerable;
                        if (enumerable != null)
                            foreach (var item in enumerable)
                            {
                                var vcm = item as VariableControllerMaster;
                                if (vcm != null) result.Add(vcm);
                            }
                    }
                }
                catch { }
            }

            // 프로퍼티도
            foreach (var p in objType.GetProperties(bf))
            {
                if (!p.CanRead) continue;
                try
                {
                    if (typeof(VariableControllerMaster).IsAssignableFrom(p.PropertyType))
                    {
                        var vcm = p.GetValue(obj) as VariableControllerMaster;
                        if (vcm != null) result.Add(vcm);
                    }
                    else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)
                             && !p.PropertyType.IsValueType && p.PropertyType != typeof(string))
                    {
                        var enumerable = p.GetValue(obj) as System.Collections.IEnumerable;
                        if (enumerable != null)
                            foreach (var item in enumerable)
                            {
                                var vcm = item as VariableControllerMaster;
                                if (vcm != null) result.Add(vcm);
                            }
                    }
                }
                catch { }
            }

            return result;
        }

        // ============================================================
        // Auto Tune (VRFT)
        // ============================================================

        /// <summary>
        /// [자동 튜닝] 버튼 클릭 시 호출.
        /// 가진 설정을 자동으로 잡고 → 녹화 시작.
        /// 이후 OnUiFixed가 매 틱마다 데이터를 모으다가 MinSamples 도달하면 자동으로 계산.
        /// </summary>
        private void AutoTuneNow()
        {
            if (_autoState != AutoTuneState.Idle && _autoState != AutoTuneState.Done && _autoState != AutoTuneState.Failed)
            {
                _sess.LastMessage = "Tuning already in progress / 튜닝 이미 진행 중";
                return;
            }

            RestoreSetPointAdjustIfNeeded();

            // 바로 녹화 시작 (Desaturation 제거 — VRFT는 기존 제어기와 무관)
            StartAutoTuneRecording();
        }

        /// <summary>
        /// 포화 해소 완료 후 실제 녹화를 시작하는 메서드.
        /// </summary>
        private void StartAutoTuneRecording()
        {
            double dt = Time.fixedDeltaTime;
            if (dt <= 0) dt = 0.02;
            double fs = 1.0 / dt;

            // 자연 변동 측정 → 시작 진폭 결정
            double naturalStd = 0;
            if (_naturalYCount >= 10)
            {
                double sum = 0;
                double sqSum = 0;
                int n = Math.Min(_naturalYCount, NaturalBufSize);
                for (int i = 0; i < n; i++)
                    sum += _naturalYBuf[i];
                double mean = sum / n;
                for (int i = 0; i < n; i++)
                {
                    double d = _naturalYBuf[i] - mean;
                    sqSum += d * d;
                }
                naturalStd = Math.Sqrt(sqSum / Math.Max(1, n - 1));
            }
            _sess.NaturalYStd = naturalStd;

            // 시작 진폭: 자연 변동의 3배 이상, 최소 0.3
            double startAmp = Math.Max(0.3, naturalStd * 3.0);
            startAmp = Math.Min(startAmp, _s.AdaptiveAmpMax);

            // SP 가진: SetPointAdjust에 멀티사인 추가, 원본 PID는 그대로 동작
            _s.ExciteEnabled = true;
            _s.ExciteWave = WaveType.MultiSine;
            _s.ExciteAmp = (float)startAmp;
            _s.ExciteFreqHz = 0.05f;
            _s.ChirpEndHz = (float)Math.Min(fs / 4.0, 2.0);
            _s.AdaptiveAmp = true;

            _autoState = AutoTuneState.Recording;
            StartRecording();
            _sess.AdaptiveCurrentAmp = _s.ExciteAmp;
            _sess.LastMessage = $"Recording (SP excite, amp={startAmp:0.00}) / 녹화 중 (SP 가진)";
        }

        /// <summary>
        /// 녹화 완료 후 다음 틱에 호출. 전체 자동 튜닝 계산 파이프라인:
        /// 1) 최장 연속 블록 선택 (포화 구멍 없는 구간)
        /// 2) 데이터 품질 체크 (포화율, y 변화량)
        /// 3) 지연시간 tau, 정착시간 Ts 자동 추정
        /// 4) VRFT 계산 → Kp, Ti, Td 결과
        /// </summary>
        private void AutoTuneCompute()
        {
            double dt = Time.fixedDeltaTime;
            if (dt <= 0) dt = 0.02;
            double fs = 1.0 / dt;

            // 최장 연속 블록 선택 (포화로 끊긴 구간 제외)
            var (blkStart, blkLen) = PickLongestBlock(_sess.BlockStarts, _sess.U.Count, _sess.Y);
            if (blkLen < 64)
            {
                _autoState = AutoTuneState.Failed;
                _sess.LastMessage = $"Auto-tune failed: best block only {blkLen} samples. Reduce saturation. / 자동 튜닝 실패: 최장 블록 {blkLen}샘플.";
                return;
            }

            double[] u = new double[blkLen];
            double[] y = new double[blkLen];
            _sess.U.CopyTo(blkStart, u, 0, blkLen);
            _sess.Y.CopyTo(blkStart, y, 0, blkLen);

            // BLA 용 r (가진 신호) — 같은 블록 구간 추출. R 가 부족하면 빈 배열
            double[] r = (_sess.R.Count >= blkStart + blkLen) ? new double[blkLen] : Array.Empty<double>();
            if (r.Length == blkLen) _sess.R.CopyTo(blkStart, r, 0, blkLen);

            // 데이터 품질 체크
            double satRatio = (double)_sess.SaturatedCount / Math.Max(1, _sess.SaturatedCount + _sess.U.Count);
            if (satRatio > 0.5)
            {
                _autoState = AutoTuneState.Failed;
                _sess.LastMessage = $"Auto-tune failed: saturation ratio {satRatio:P0}. Reduce amplitude. / 자동 튜닝 실패: 포화 비율 {satRatio:P0}.";
                return;
            }

            double yStd = StdDev(y);
            if (yStd < 1e-6)
            {
                _autoState = AutoTuneState.Failed;
                _sess.LastMessage = "Auto-tune failed: no change in y. Check PID connection. / 자동 튜닝 실패: y 변화 없음.";
                return;
            }

            // 디버그: u, y 범위 기록
            double uMin = u[0], uMax = u[0];
            double yMin = y[0], yMax = y[0];
            for (int i = 1; i < blkLen; i++)
            {
                if (u[i] < uMin) uMin = u[i];
                if (u[i] > uMax) uMax = u[i];
                if (y[i] < yMin) yMin = y[i];
                if (y[i] > yMax) yMax = y[i];
            }
            _sess.LastMessage = $"Data: u=[{uMin:0.000},{uMax:0.000}] y=[{yMin:0.0},{yMax:0.0}] yStd={yStd:0.000}";

            // τ = dt 고정 (FTD 순수 지연 ≈ 1틱)
            // 폐루프에서 위상 기울기 추정은 PID 위상을 포함해 과대추정하므로 사용하지 않음
            _s.ModelDelayTau = (float)dt;

            // 고정 파라미터
            _s.CutoffHz = (float)(fs / 8.0);

            // Ts 자동 탐색: 파라미터 안정성 기반 Ts 선택
            // nM=2 고정 (FTD 제어 대상은 대부분 2차 시스템)
            _s.ModelOrderNm = 2;
            VrftResult bestResult = default;
            double bestTs = 1.0;
            bool anyFound = false;

            // 파라미터 안정성 기반 Ts 선택
            // Ts를 스캔하면서 Kp, Ti, Td를 기록하고,
            // 인접 Ts 간 파라미터 변화율이 작은(안정적인) 가장 작은 Ts를 선택
            int tsSteps = 40;
            double[] tsArr = new double[tsSteps + 1];
            double[] kpArr = new double[tsSteps + 1];
            double[] tiArr = new double[tsSteps + 1];
            double[] tdArr = new double[tsSteps + 1];
            bool[] validArr = new bool[tsSteps + 1];
            VrftResult[] results = new VrftResult[tsSteps + 1];

            for (int si = 0; si <= tsSteps; si++)
            {
                tsArr[si] = 0.1 * Math.Pow(10.0, (double)si / tsSteps); // 0.1 ~ 1.0
                _s.SettlingTimeTs = (float)tsArr[si];
                results[si] = ComputeVrftPid(u, y, dt, _s);
                kpArr[si] = results[si].Kp;
                tiArr[si] = results[si].Ti;
                tdArr[si] = results[si].Td;
                validArr[si] = results[si].Kp > 0;
            }

            // 인접 점 간 상대 변화율 = |p[i+1]-p[i]| / max(|p[i]|, eps)
            // Kp, Ti, Td 변화율의 최대값이 임계값 이하인 가장 작은 Ts
            double stabilityThreshold = 0.3; // 30% 이하 변화면 안정
            for (int si = 0; si < tsSteps; si++)
            {
                if (!validArr[si] || !validArr[si + 1]) continue;

                double dKp = Math.Abs(kpArr[si + 1] - kpArr[si]) / Math.Max(Math.Abs(kpArr[si]), 1e-6);
                double dTi = Math.Abs(tiArr[si + 1] - tiArr[si]) / Math.Max(Math.Abs(tiArr[si]), 1e-6);
                double dTd = Math.Abs(tdArr[si + 1] - tdArr[si]) / Math.Max(Math.Abs(tdArr[si]), 1e-6);
                double maxChange = Math.Max(dKp, Math.Max(dTi, dTd));

                if (maxChange < stabilityThreshold)
                {
                    bestResult = results[si];
                    bestTs = tsArr[si];
                    anyFound = true;
                    break;
                }
            }

            // 안정 영역 못 찾으면 Kp > 0인 가장 큰 Ts fallback
            if (!anyFound)
            {
                for (int si = tsSteps; si >= 0; si--)
                {
                    if (validArr[si])
                    {
                        bestResult = results[si];
                        bestTs = tsArr[si];
                        anyFound = true;
                        break;
                    }
                }
            }

            // 전체 실패 시 fallback
            if (!anyFound)
            {
                _s.ModelOrderNm = 2;
                _s.SettlingTimeTs = 1.0f;
                bestResult = ComputeVrftPid(u, y, dt, _s);
                bestTs = 1.0;
            }

            _s.SettlingTimeTs = (float)bestTs;
            VrftResult vrft = bestResult;

            // 3-way 식별: PEM / BLA / VRFT 모두 계산 후 신뢰도로 정렬.
            //   PEM identRatio (innovation RMS / std(y)): 낮을수록 신뢰
            //   BLA identRatio (1 - mean coherence): 낮을수록 신뢰 (coherence 높음)
            //   VRFT: 항상 가능 (신뢰도 메트릭 없음, 폴백)
            // 우선순위: 가장 낮은 IdentRatio 를 가진 모델이 주력, 차순위가 대안.
            const double IDENT_THRESHOLD = 0.5; // 이보다 크면 신뢰 X (VRFT로 폴백)

            ModelPidResult pemResult = default;
            ModelPidResult blaResult = default;
            bool pemOk = false, blaOk = false;
            string pemInfo = "", blaInfo = "";

            try { pemResult = ComputePemPid(u, y, dt); pemOk = true; pemInfo = pemResult.ModelInfo; }
            catch (Exception ex) { pemInfo = $"PEM failed: {ex.Message}"; }

            // BLA 는 r 데이터 있을 때만
            if (r.Length == blkLen)
            {
                try { blaResult = ComputeBlaPid(u, y, r, dt, _s); blaOk = true; blaInfo = blaResult.ModelInfo; }
                catch (Exception ex) { blaInfo = $"BLA failed: {ex.Message}"; }
            }
            else
            {
                blaInfo = "BLA skipped: no excitation data";
            }

            string vrftInfo = $"VRFT Ts={bestTs:0.00} rmse={vrft.Rmse:0.00e0}";

            // 후보들 점수 매김: (이름, IdentRatio, Result, Info)
            var candidates = new List<(string name, double score, double Kp, double Ti, double Td, string info)>();
            if (pemOk && pemResult.IdentRatio < IDENT_THRESHOLD)
                candidates.Add(("PEM", pemResult.IdentRatio, pemResult.Kp, pemResult.Ti, pemResult.Td, pemInfo));
            if (blaOk && blaResult.IdentRatio < IDENT_THRESHOLD)
                candidates.Add(("BLA", blaResult.IdentRatio, blaResult.Kp, blaResult.Ti, blaResult.Td, blaInfo));
            // VRFT 는 항상 후보 (낮은 우선순위로 score=0.99)
            candidates.Add(("VRFT", 0.99, vrft.Kp, vrft.Ti, vrft.Td, vrftInfo));

            candidates.Sort((a, b) => a.score.CompareTo(b.score));

            // 주력 = 1순위, 대안 = 2순위 (있으면)
            var primary = candidates[0];
            string primaryName = primary.name;
            string primaryInfo = primary.info;
            double primaryKp = primary.Kp, primaryTi = primary.Ti, primaryTd = primary.Td;

            string altName = "", altInfo = "";
            double altKp = 0, altTi = 0, altTd = 0;
            bool hasAlt = candidates.Count >= 2;
            if (hasAlt)
            {
                var alt = candidates[1];
                altName = alt.name; altInfo = alt.info;
                altKp = alt.Kp; altTi = alt.Ti; altTd = alt.Td;
            }

            _sess.HasResult = true;
            _sess.Kp = primaryKp; _sess.Ti = primaryTi; _sess.Td = primaryTd;
            _sess.ActiveMethodName = primaryName;
            _sess.ActiveMethodInfo = primaryInfo;
            _sess.HasAlt = hasAlt;
            _sess.AltKp = altKp; _sess.AltTi = altTi; _sess.AltTd = altTd;
            _sess.AltMethodName = altName; _sess.AltMethodInfo = altInfo;
            _sess.FitRmse = vrft.Rmse;

            _autoState = AutoTuneState.Done;
            string swapHint = hasAlt ? $" | alt {altName}: Kp={altKp:0.000} Ti={altTi:0.1} Td={altTd:0.00}" : "";
            _sess.LastMessage = $"Done | {primaryName}: Kp={primaryKp:0.000} Ti={primaryTi:0.1} Td={primaryTd:0.00}{swapHint}";
        }

        /// <summary>주 결과와 대안 결과를 swap (사용자가 다른 방법 결과를 보고 싶을 때).</summary>
        private void SwapPidMethod()
        {
            if (!_sess.HasResult || !_sess.HasAlt) return;
            double k = _sess.Kp, i = _sess.Ti, d = _sess.Td;
            string n = _sess.ActiveMethodName, info = _sess.ActiveMethodInfo;
            _sess.Kp = _sess.AltKp; _sess.Ti = _sess.AltTi; _sess.Td = _sess.AltTd;
            _sess.ActiveMethodName = _sess.AltMethodName; _sess.ActiveMethodInfo = _sess.AltMethodInfo;
            _sess.AltKp = k; _sess.AltTi = i; _sess.AltTd = d;
            _sess.AltMethodName = n; _sess.AltMethodInfo = info;
            _sess.LastMessage = $"Swapped → {_sess.ActiveMethodName}: Kp={_sess.Kp:0.000} Ti={_sess.Ti:0.1} Td={_sess.Td:0.00}";
        }

        private void CaptureSetPointAdjustBase()
        {
            try
            {
                _baseSetPointAdjust = this._focus.SetPointAdjust.Us;
                _hasBaseSetPointAdjust = true;
            }
            catch
            {
                _hasBaseSetPointAdjust = false;
                _sess.LastMessage = "SetPointAdjust access failed. Excitation may not work. / SetPointAdjust 접근 실패.";
            }
        }

        private void RestoreSetPointAdjustIfNeeded()
        {
            if (!_hasBaseSetPointAdjust) return;
            try { this._focus.SetPointAdjust.Us = _baseSetPointAdjust; }
            catch { }
        }

        /// <summary>
        /// 매 틱마다 SetPoint에 가진 신호를 더함.
        /// SP = 원래SP + x(t) 형태로, PID가 이 변화에 반응하게 만들어서
        /// 플랜트의 동특성 정보를 u/y 데이터에 담기 위한 것.
        ///
        /// 가진이 왜 필요한가?
        /// - PID가 안정적으로 잘 작동하면 u/y가 거의 일정 → 플랜트 정보 없음
        /// - 외부에서 SP를 흔들어야 PID가 반응하고, 그 반응에서 플랜트 특성이 드러남
        /// </summary>
        /// <summary>현재 틱 가진값 (BLA 폐루프 식별용 외생 입력 r). 비활성/조건 미충족 시 0.</summary>
        private double _lastExciteValue = 0.0;

        private void ApplyExcitation(float dt)
        {
            _lastExciteValue = 0.0;
            if (!_s.ExciteEnabled) return;        // 가진 꺼져있으면 무시
            if (_s.ExciteWave == WaveType.Off) return;
            if (!_hasBaseSetPointAdjust) return;   // 원래 SP를 백업 못 했으면 무시

            double t = _sess.T - _sess.BlockStartT;  // 현재 블록 시작부터의 경과 시간 (chirp 리셋)
            double amp = Math.Max(0.0, _s.ExciteAmp); // 진폭 (음수 방지)

            // 포화 회피: |u|가 포화 임계값에 가까우면 가진 진폭을 줄임
            double absU = Math.Abs(_sess.LastU);
            double satMargin = _s.SaturationThreshold - absU; // 포화까지 남은 여유
            if (satMargin < 0.3 && satMargin > 0.0)
            {
                // 여유가 0.3~0 → 스케일 1.0~0.1
                double scale = Math.Max(0.1, satMargin / 0.3);
                amp *= scale;
            }
            else if (satMargin <= 0.0)
            {
                amp *= 0.1; // 이미 포화 근처면 최소 가진
            }

            double x = 0.0;

            switch (_s.ExciteWave)
            {
                case WaveType.Sine:
                    {
                        double w = 2.0 * Math.PI * Math.Max(0.01, _s.ExciteFreqHz);
                        x = amp * Math.Sin(w * t);
                        break;
                    }
                case WaveType.Chirp:
                    {
                        // 로그 chirp - 저주파에서 오래 머물러 Ti 추정에 유리
                        // 순시 주파수: f(t) = f0 * (f1/f0)^(t/T)
                        // 위상: φ(t) = 2π * f0*T/ln(f1/f0) * ((f1/f0)^(t/T) - 1)
                        double f0 = Math.Max(0.01, _s.ChirpStartHz);
                        double f1 = Math.Max(f0 * 1.1, _s.ChirpEndHz);
                        double T = Math.Max(1.0, _s.MinSamples * dt);
                        double ratio = f1 / f0;
                        double lnRatio = Math.Log(ratio);
                        double phase;
                        if (t <= T)
                        {
                            phase = 2.0 * Math.PI * f0 * T / lnRatio * (Math.Pow(ratio, t / T) - 1.0);
                        }
                        else
                        {
                            double phaseAtT = 2.0 * Math.PI * f0 * T / lnRatio * (ratio - 1.0);
                            phase = phaseAtT + 2.0 * Math.PI * f1 * (t - T);
                        }
                        x = amp * Math.Sin(phase);
                        break;
                    }
                case WaveType.MultiSine:
                    {
                        // 12성분 멀티사인, 로그 간격 (0.05~5Hz), Schroeder 위상
                        // 산업용 시스템 식별 표준 방식
                        double fBase = Math.Max(0.01, _s.ExciteFreqHz);
                        double fMax = Math.Max(fBase * 2.0, _s.ChirpEndHz);
                        int nComp = 12;
                        double compAmp = amp / Math.Sqrt(nComp); // RMS 진폭 유지
                        for (int ci = 0; ci < nComp; ci++)
                        {
                            // 로그 간격 주파수 배치
                            double fi = fBase * Math.Pow(fMax / fBase, (double)ci / (nComp - 1));
                            // Schroeder 위상: 피크 팩터 최소화
                            double phi = -Math.PI * ci * (ci + 1) / nComp;
                            x += compAmp * Math.Sin(2.0 * Math.PI * fi * t + phi);
                        }
                        break;
                    }
            }

            _lastExciteValue = x;
            try
            {
                this._focus.SetPointAdjust.Us = _baseSetPointAdjust + (float)x;
            }
            catch { }
        }

        // ============================================================
        // Compute / Apply
        // ============================================================

        private struct VrftResult
        {
            public double Kp, Ti, Td, Rmse;
            public string Warning;
        }

        private void ComputeNow()
        {
            try
            {
                double dt = Time.fixedDeltaTime;
                if (dt <= 0) dt = 0.02;

                // 최장 연속 블록 선택
                var (blkStart, blkLen) = PickLongestBlock(_sess.BlockStarts, _sess.U.Count, _sess.Y);
                if (blkLen < _s.MinSamples)
                {
                    _sess.LastMessage = $"Insufficient: best block {blkLen}/{_s.MinSamples} / 샘플 부족: 최장 블록 {blkLen}/{_s.MinSamples}";
                    return;
                }

                double[] u = new double[blkLen];
                double[] y = new double[blkLen];
                _sess.U.CopyTo(blkStart, u, 0, blkLen);
                _sess.Y.CopyTo(blkStart, y, 0, blkLen);

                VrftResult r = ComputeVrftPid(u, y, dt, _s);

                _sess.HasResult = true;
                _sess.Kp = r.Kp;
                _sess.Ti = r.Ti;
                _sess.Td = r.Td;
                _sess.FitRmse = r.Rmse;

                _sess.LastMessage = string.IsNullOrEmpty(r.Warning)
                    ? "Computation complete / 계산 완료"
                    : "Computation complete (warning: " + r.Warning + ") / 계산 완료";
            }
            catch (Exception e)
            {
                _sess.LastMessage = "Computation failed / 계산 실패: " + e.Message;
            }
        }

        private void ApplyToPid()
        {
            try
            {
                if (!_sess.HasResult)
                {
                    _sess.LastMessage = "No result. Compute first. / 결과가 없습니다.";
                    return;
                }

                // 최소 단위 반영
                double kp = RoundToStep(Math.Max(0.001, _sess.Kp), 0.001);
                double ti = RoundToStep(Math.Max(0.0, _sess.Ti), 0.1);
                double td = RoundToStep(Math.Max(0.0, _sess.Td), 0.01);

                // 게임 UI 관례 (kI가 250이면 off처럼 쓰는 경우가 많음)
                if (ti > 250.0) ti = 250.0;
                if (td > 10.0) td = 10.0;

                this._focus.Pid.kP.Us = (float)kp;
                this._focus.Pid.kI.Us = (float)ti; // Ti
                this._focus.Pid.kD.Us = (float)td; // Td

                _sess.LastMessage = $"Applied: Kp={kp:0.000}, Ti={ti:0.0}, Td={td:0.00}";
            }
            catch (Exception e)
            {
                _sess.LastMessage = "Apply failed / 적용 실패: " + e.Message;
            }
        }

        /// <summary>
        /// ★ VRFT 핵심 계산 함수 ★
        ///
        /// 입력: u[](제어 출력), y[](프로세스 변수), dt(샘플 간격), s(설정)
        /// 출력: VrftResult {Kp, Ti, Td, Rmse, Warning}
        ///
        /// static = 인스턴스 없이 호출 가능 (클래스 메서드). 외부 상태에 의존 안 함.
        ///
        /// 전체 흐름:
        /// [u, y] → 디트렌드 → 제로패딩 FFT → M,W,F 필터 → 가상에러 ev
        /// → IFFT → 사다리꼴적분/중심차분 → 컬럼스케일링 → TLS(SVD) → Kp, Ti, Td
        /// </summary>
        private static VrftResult ComputeVrftPid(double[] u, double[] y, double dt, Settings s)
        {
            int N = Math.Min(u.Length, y.Length);
            if (N < 64) throw new Exception("Too few samples / 샘플이 너무 적습니다.");

            // ── 1단계: 전처리 ──
            // 디트렌드 = DC(평균값) + 선형 추세를 빼는 것.
            // 왜? FFT는 "주기적 신호"를 가정하는데, 데이터에 추세가 있으면
            //      시작과 끝이 안 맞아서 모든 주파수에 노이즈가 퍼짐 (spectral leakage).
            // Array.Copy = 원본을 보존하기 위해 복사본을 만들어서 처리.
            double[] ud = new double[N]; Array.Copy(u, ud, N); Detrend(ud);
            double[] yd = new double[N]; Array.Copy(y, yd, N); Detrend(yd);

            // ── 2단계: FFT (시간→주파수 변환) ──
            // 제로패딩: 데이터 뒤에 0을 붙여서 FFT 크기를 2N 이상으로.
            // 왜? FFT로 필터링하면 "순환 합성곱"이 되는데, 제로패딩하면
            //      "선형 합성곱"과 같아져서 시작/끝 아티팩트가 사라짐.
            // NextPow2 = 2의 거듭제곱으로 올림 (FFT가 효율적으로 작동하는 크기)
            int Nfft = NextPow2(2 * N);
            Complex[] U = new Complex[Nfft];
            Complex[] Y = new Complex[Nfft];
            for (int i = 0; i < N; i++)
            {
                U[i] = new Complex(ud[i], 0);
                Y[i] = new Complex(yd[i], 0);
            }
            // [N..Nfft) 은 0 → 제로패딩

            Fourier.Forward(U, FourierOptions.Matlab);
            Fourier.Forward(Y, FourierOptions.Matlab);

            double fs = 1.0 / dt;

            double ts = Math.Max(0.05, s.SettlingTimeTs);
            int nM = ClampInt(s.ModelOrderNm, 1, 10);
            double tauM = Math.Max(0.0, s.ModelDelayTau);

            double wW = 2.0 * Math.PI * Math.Max(0.01, s.CutoffHz); // rad/s

            Complex[] UF = new Complex[Nfft];
            Complex[] EF = new Complex[Nfft];

            // ── 3단계: 주파수 영역에서 VRFT 필터 적용 ──
            // 각 주파수 빈(bin)마다 M, W, F를 계산하고, 가상 에러를 구함.
            for (int k = 0; k < Nfft; k++)
            {
                // FFT 출력의 인덱스 → 실제 주파수 변환:
                // k=0 → DC(0Hz), k=1 → fs/Nfft Hz, ..., k=Nfft/2 → Nyquist
                // k > Nfft/2 → 음의 주파수 (대칭)
                int ks = (k <= Nfft / 2) ? k : (k - Nfft);   // 부호 있는 인덱스
                double f = (double)ks * fs / (double)Nfft;    // 주파수 (Hz)
                double w = 2.0 * Math.PI * f;                 // 각주파수 (rad/s)
                Complex jw = new Complex(0, w);               // jw = 허수 * w (라플라스 변환의 s=jw)

                // (3.0.17) 참조모델 M: "폐루프가 이렇게 반응했으면 좋겠다"
                // 분자: exp(-jw*tau) = 순수 지연 (tau초 뒤에 반응 시작)
                // 분모: (1 + jw*0.2*ts)^nM = nM차 저역통과 (부드럽게 감쇠)
                Complex denom = Complex.Pow(Complex.One + jw * (0.2 * ts), nM);
                Complex Mf = Complex.Exp(-jw * tauM) / denom;

                // (3.0.18) 가중 필터 W: 1차 저역통과. 고주파 노이즈 억제.
                Complex W = (wW) / (jw + wW);

                // (2.3.11) 모델 매칭 필터 F = M*(1-M)*W
                // 밴드패스 특성: DC에서 0 (M≈1이면 1-M≈0), 고주파에서 0 (W가 억제)
                Complex F = Mf * (Complex.One - Mf) * W;

                // 가상 레퍼런스: "이 y가 M 폐루프에서 나왔다면, 입력은 뭐였을까?"
                // rv = Y / M (M의 역) → 가상 에러 ev = rv - Y
                Complex rv = (Mf.Magnitude < 1e-12) ? Complex.Zero : (Y[k] / Mf);
                Complex ev = rv - Y[k];

                // F로 필터링: uF = F*U, eF = F*ev
                UF[k] = F * U[k];
                EF[k] = F * ev;
            }

            // ── 4단계: IFFT (주파수→시간 복원) ──
            Fourier.Inverse(UF, FourierOptions.Matlab);
            Fourier.Inverse(EF, FourierOptions.Matlab);

            // 제로패딩했으므로 유효 구간은 원래 데이터 길이 [0, N)만 사용
            double[] uF = new double[N];
            double[] eF = new double[N];
            for (int i = 0; i < N; i++)
            {
                uF[i] = UF[i].Real;
                eF[i] = EF[i].Real;
            }

            int drop = ClampInt(s.DropEdgeSamples, 0, N / 3);
            int i0 = drop;
            int i1 = N - drop;
            int L = i1 - i0;

            if (L < 64) throw new Exception("Drop too large, insufficient valid samples / Drop이 너무 큼.");

            // ── 5단계: PID 회귀 벡터 구성 ──
            // 목표: uF ≈ Kp*eF + (Kp/Ti)*∫eF + Kp*Td*d(eF)/dt
            // 즉:   bv  ≈ rho1*phi1 + rho2*phi2 + rho3*phi3
            //
            // phi1 = eF 그 자체          → P항 (비례)
            // phi2 = eF의 적분           → I항 (적분)
            // phi3 = eF의 미분           → D항 (미분)
            // bv   = uF (맞춰야 할 목표)
            double[] phi1 = new double[L]; // P항: eF
            double[] phi2 = new double[L]; // I항: ∫eF (사다리꼴 적분)
            double[] phi3 = new double[L]; // D항: d(eF)/dt (중심 차분)
            double[] bv = new double[L];   // 목표: uF

            // 사다리꼴 적분 (Tustin): (eF[i-1]+eF[i])/2 * dt
            // 전방 Euler보다 정확 (O(dt²) vs O(dt))
            double integ = 0.0;
            for (int i = 0; i < L; i++)
            {
                int idx = i0 + i;
                if (i > 0)
                    integ += 0.5 * (eF[idx - 1] + eF[idx]) * dt;

                phi1[i] = eF[idx];
                phi2[i] = integ;
                bv[i] = uF[idx];
            }

            // 중심 차분: (eF[i+1]-eF[i-1]) / (2*dt)
            // 후방 차분 (eF[i]-eF[i-1])/dt 보다 정확 (O(dt²))
            // 첫/끝 점은 중심 차분 불가 → 전방/후방 차분 사용
            for (int i = 0; i < L; i++)
            {
                int idx = i0 + i;
                if (i == 0)
                    phi3[i] = (eF[idx + 1] - eF[idx]) / dt;
                else if (i == L - 1)
                    phi3[i] = (eF[idx] - eF[idx - 1]) / dt;
                else
                    phi3[i] = (eF[idx + 1] - eF[idx - 1]) / (2.0 * dt);
            }

            // ── 6단계: 컬럼 스케일링 ──
            // phi1, phi2, phi3��� 크기(스케일)가 완전히 다름:
            //   phi1 ~ O(1), phi2 ~ O(수십, 적분이라 누적), phi3 ~ O(50, 미분이라 1/dt)
            // SVD/TLS는 모든 열이 비슷한 크기라고 가정 → 스케일링 필수.
            // RMS (Root Mean Square) = sqrt(sum(x²)/N) 로 각 열을 정규화.
            double scale1 = 0, scale2 = 0, scale3 = 0, scaleB = 0;
            for (int i = 0; i < L; i++)
            {
                scale1 += phi1[i] * phi1[i];
                scale2 += phi2[i] * phi2[i];
                scale3 += phi3[i] * phi3[i];
                scaleB += bv[i] * bv[i];
            }
            scale1 = scale1 > 0 ? Math.Sqrt(scale1 / L) : 1.0;
            scale2 = scale2 > 0 ? Math.Sqrt(scale2 / L) : 1.0;
            scale3 = scale3 > 0 ? Math.Sqrt(scale3 / L) : 1.0;
            scaleB = scaleB > 0 ? Math.Sqrt(scaleB / L) : 1.0;

            double invS1 = 1.0 / scale1, invS2 = 1.0 / scale2, invS3 = 1.0 / scale3, invSB = 1.0 / scaleB;

            // ── 7단계: 조건수 진단 ──
            // 조건수 = 최대 특이값 / 최소 특이값.
            // 큰 조건수(>10^6) = 행렬이 거의 특이(singular) → 해가 불안정.
            // 원인: 가진이 부족하거나, 데이터가 특정 방향으로만 분포.
            Matrix<double> A = MB.Dense(L, 3);
            for (int i = 0; i < L; i++)
            {
                A[i, 0] = phi1[i] * invS1;
                A[i, 1] = phi2[i] * invS2;
                A[i, 2] = phi3[i] * invS3;
            }
            var svdA = A.Svd();
            double sMax = svdA.S[0];
            double sMin = svdA.S[svdA.S.Count - 1];
            double condNum = sMin > 1e-15 ? sMax / sMin : double.PositiveInfinity;

            // ── 8단계: OLS (스케일링된 QR 최소자승) ──
            // bv ≈ rho1*phi1 + rho2*phi2 + rho3*phi3 을 최소자승으로 풀기.
            // QR 분해: A = Q*R → rho = R^{-1} * Q^T * b (수치적으로 안정)
            //
            // 왜 TLS 대신 OLS인가?
            //   TLS는 모든 열에 같은 노이즈를 가정하지만, VRFT에서는
            //   phi(=eF 유래)와 bv(=uF 유래)의 노이즈 구조가 다름.
            //   → TLS가 해를 0 방향으로 축소(shrinkage) → Kp 과소추정.
            //   FTD는 게임이라 측정 노이즈가 거의 없어서 OLS 편향도 미미.
            var bScaled = VB.DenseOfArray(bv).Multiply(invSB);
            var rhoScaled = A.QR().Solve(bScaled);
            double Kp = rhoScaled[0] * (scaleB / scale1);
            double aI = rhoScaled[1] * (scaleB / scale2);
            double aD = rhoScaled[2] * (scaleB / scale3);

            if (double.IsNaN(Kp) || double.IsInfinity(Kp)) Kp = 0.0;
            if (double.IsNaN(aI) || double.IsInfinity(aI)) aI = 0.0;
            if (double.IsNaN(aD) || double.IsInfinity(aD)) aD = 0.0;

            string warning = null;

            if (condNum > 1e6)
                warning = $"High regression condition number ({condNum:0.0e0}). Check excitation. / 회귀 조건수가 높습니다({condNum:0.0e0}).";

            if (Kp < 0)
            {
                warning = (warning == null ? "" : warning + " / ") +
                          $"Kp regression is negative ({Kp:0.000}). Phase inversion or poor data quality. / Kp 음수({Kp:0.000}).";
                Kp = 0;
            }

            double nyquist = fs / 2.0;
            if (s.CutoffHz > nyquist)
                warning = (warning == null ? "" : warning + " / ") +
                          $"f_W({s.CutoffHz:0.0}Hz) exceeds Nyquist({nyquist:0.0}Hz). Lower f_W. / f_W가 Nyquist를 초과합니다.";

            // ── Ti 결정 ──
            // F=M(1-M)W는 DC에서 0 → aI 추정이 구조적으로 부정확.
            // VRFT 값이 합리적이면 사용, 아니면 IMC 규칙 fallback.
            // IMC: Ti = 플랜트 시정수 ≈ M의 시정수 × nM = 0.2*Ts*2 = 0.4*Ts
            double Ti_imc = 0.4 * Math.Max(0.2, s.SettlingTimeTs);
            double Ti_vrft = 250.0;
            if (aI > 1e-12 && Kp > 1e-12) Ti_vrft = Kp / aI;

            double Ti;
            if (Ti_vrft > 0.1 && Ti_vrft < 100.0)
                Ti = Ti_vrft;  // VRFT 추정이 합리적 범위
            else
                Ti = Ti_imc;   // IMC 규칙 fallback

            if (Ti < 0.1) Ti = 0.1;
            if (Ti > 250.0) Ti = 250.0;

            double Td = 0.0;
            if (Kp > 1e-12) Td = aD / Kp;
            if (Td < 0) Td = 0;

            // RMSE
            double sse = 0.0;
            for (int i = 0; i < L; i++)
            {
                double pred = Kp * phi1[i] + aI * phi2[i] + aD * phi3[i];
                double err = bv[i] - pred;
                sse += err * err;
            }
            double rmse = Math.Sqrt(sse / Math.Max(1, L));

            return new VrftResult { Kp = Kp, Ti = Ti, Td = Td, Rmse = rmse, Warning = warning };
        }

        // ============================================================
        // MISO N4SID 서브스페이스 모델 식별
        // 다중 입력(자기축 u + 타축 u) → 단일 출력(y) → 커플링 분리
        // Hankel 행렬 → SVD → 상태공간(A,B,C,D) → IMC-PID (자기축만)
        // ============================================================

        private struct ModelPidResult
        {
            public double Kp, Ti, Td;
            public string ModelInfo;
            public double IdentRatio;  // innovation RMS / std(y) — 낮을수록 모델 신뢰 ↑ (PEM만)
        }

        private static ModelPidResult ComputeModelPid(double[] u, double[] y, double dt, double[][] otherU = null)
        {
            _ = otherU; // MISO 확장용 예약 (현재 SISO)
            int N = Math.Min(u.Length, y.Length);
            if (N < 100) throw new Exception("Too few samples for N4SID");

            // ── 1. 디트렌드 (DC + 선형 추세 제거) ──
            double[] ud = new double[N]; Array.Copy(u, ud, N); Detrend(ud);
            double[] yd = new double[N]; Array.Copy(y, yd, N); Detrend(yd);

            // ── 2. 블록 Hankel 행렬 (균형 과거/미래 horizon i) ──
            int iHor = Math.Min(20, (N - 1) / 4);
            if (iHor < 4) throw new Exception("Horizon too small");
            int j = N - 2 * iHor + 1;
            if (j < 2 * iHor + 10) throw new Exception("Insufficient columns for N4SID");

            var Up = MB.Dense(iHor, j);
            var Uf = MB.Dense(iHor, j);
            var Yp = MB.Dense(iHor, j);
            var Yf = MB.Dense(iHor, j);
            for (int col = 0; col < j; col++)
                for (int row = 0; row < iHor; row++)
                {
                    Up[row, col] = ud[col + row];
                    Yp[row, col] = yd[col + row];
                    Uf[row, col] = ud[col + iHor + row];
                    Yf[row, col] = yd[col + iHor + row];
                }

            // 과거 입출력 결합 W_p = [U_p; Y_p] (2i × j) — 상태 재구성용
            var Wp = MB.Dense(2 * iHor, j);
            for (int row = 0; row < iHor; row++)
                for (int col = 0; col < j; col++)
                {
                    Wp[row, col] = Up[row, col];
                    Wp[row + iHor, col] = Yp[row, col];
                }

            // ── 3. 경사 투영 O_i = Y_f /_{U_f} W_p ──
            // 원리 (Van Overschee 1994): U_f 방향을 따라 W_p 행공간에 Y_f를 투영.
            // 공식: O_i = Y_f · Π_{U_f^⊥} · (W_p · Π_{U_f^⊥})^† · W_p
            //   Π_{U_f^⊥} = I - U_f^T (U_f U_f^T)^{-1} U_f  (U_f 직교여집합 투영)
            // wide matrix의 pinv: pinv(X) = X^T (X X^T)^{-1}
            Matrix<double> ObliqueProject(Matrix<double> YfA, Matrix<double> UfA, Matrix<double> WpA)
            {
                var UU = UfA * UfA.Transpose();                   // i × i
                var UU_inv_Uf = UU.Solve(UfA);                    // (U U^T)^-1 · U
                var UfT = UfA.Transpose();
                var YfPerp = YfA - YfA * UfT * UU_inv_Uf;         // Y_f · Π⊥
                var WpPerp = WpA - WpA * UfT * UU_inv_Uf;         // W_p · Π⊥
                // Coeff = Y_f · Π⊥ · W_p · Π⊥^T · (W_p · Π⊥ · W_p · Π⊥^T)^{-1}
                var WW = WpPerp * WpPerp.Transpose();             // 2i × 2i
                var YW = YfPerp * WpPerp.Transpose();             // i × 2i
                var Coeff = WW.Solve(YW.Transpose()).Transpose(); // i × 2i
                return Coeff * WpA;                               // i × j
            }

            var Oi = ObliqueProject(Yf, Uf, Wp);

            // ── 4. SVD + Gavish-Donoho 최적 차수 선택 ──
            // 직사각 행렬 (m × n, m=i, n=j) β = min(m,n)/max(m,n):
            //   ω(β) = √( 2(β+1) + 8β / ((β+1) + √(β² + 14β + 1)) )
            //   τ = ω(β) · σ_median  (unknown noise level)
            var svd = Oi.Svd();
            var sigma = svd.S;
            int svCnt = sigma.Count;
            double[] sigSorted = new double[svCnt];
            for (int k = 0; k < svCnt; k++) sigSorted[k] = sigma[k];
            Array.Sort(sigSorted);
            double sigMedian = sigSorted[sigSorted.Length / 2];
            double beta = (double)Math.Min(iHor, j) / Math.Max(iHor, j);
            double omegaGD = Math.Sqrt(2 * (beta + 1) + 8 * beta /
                              ((beta + 1) + Math.Sqrt(beta * beta + 14 * beta + 1)));
            double gdThreshold = omegaGD * sigMedian;

            int maxOrder = Math.Min(4, svCnt);
            int order = 1;
            for (int k = 0; k < maxOrder; k++)
            {
                if (sigma[k] < gdThreshold) break;
                order = k + 1;
            }

            // ── 5. 확장 관측성 행렬 Γ_i = U_n · Σ_n^{1/2}  (i × n) ──
            var Un = svd.U.SubMatrix(0, iHor, 0, order);
            var SigmaHalf = MB.DiagonalOfDiagonalArray(order, order,
                Enumerable.Range(0, order).Select(k => Math.Sqrt(sigma[k])).ToArray());
            var Gamma_i = Un * SigmaHalf;

            // ── 6. 상태 시퀀스 X_i = Γ_i^† · O_i  (n × j) ──
            var GT = Gamma_i.Transpose();
            Matrix<double> X_i;
            try { X_i = (GT * Gamma_i).Inverse() * GT * Oi; }
            catch { throw new Exception("Gamma pseudo-inverse failed"); }

            // ── 7. 이동(shifted) 경사 투영 → X_{i+1} 재구성 ──
            // Y_f^- = Y_f(2:i, :)   (i-1) × j
            // U_f^- = U_f(2:i, :)   (i-1) × j
            // W_p^+ = [U_p; Uf(1,:); Y_p; Yf(1,:)]  2(i+1) × j
            var Yfm = Yf.SubMatrix(1, iHor - 1, 0, j);
            var Ufm = Uf.SubMatrix(1, iHor - 1, 0, j);
            var Wpp = MB.Dense(2 * (iHor + 1), j);
            for (int row = 0; row < iHor; row++)
                for (int col = 0; col < j; col++)
                {
                    Wpp[row, col] = Up[row, col];              // 확장된 U_past: 기존 Up
                    Wpp[row + iHor + 1, col] = Yp[row, col];   // 확장된 Y_past: 기존 Yp
                }
            for (int col = 0; col < j; col++)
            {
                Wpp[iHor, col] = Uf[0, col];                   // 확장된 U_past 마지막: Uf 첫 행
                Wpp[2 * iHor + 1, col] = Yf[0, col];           // 확장된 Y_past 마지막: Yf 첫 행
            }
            var O_im1 = ObliqueProject(Yfm, Ufm, Wpp);         // (i-1) × j

            // Γ_{i-1} = Γ_i의 마지막 블록행(SISO=1행) 제거 → (i-1) × n
            var Gamma_im1 = Gamma_i.SubMatrix(0, iHor - 1, 0, order);
            var GT2 = Gamma_im1.Transpose();
            Matrix<double> X_ip1;
            try { X_ip1 = (GT2 * Gamma_im1).Inverse() * GT2 * O_im1; }
            catch { throw new Exception("Gamma_{i-1} pseudo-inverse failed"); }

            // ── 8. [A B; C D] 최소자승 추출 ──
            //   [X_{i+1}]   [A B]   [X_i    ]
            //   [Y_{i|i}] = [C D] · [U_{i|i}]
            // Θ · P = Q → Θ = Q · P^T · (P P^T)^{-1}
            var U_ii = Uf.SubMatrix(0, 1, 0, j);  // 1 × j (시점 i의 입력)
            var Y_ii = Yf.SubMatrix(0, 1, 0, j);  // 1 × j (시점 i의 출력)

            var P = MB.Dense(order + 1, j);
            for (int r = 0; r < order; r++)
                for (int c = 0; c < j; c++)
                    P[r, c] = X_i[r, c];
            for (int c = 0; c < j; c++) P[order, c] = U_ii[0, c];

            var Q = MB.Dense(order + 1, j);
            for (int r = 0; r < order; r++)
                for (int c = 0; c < j; c++)
                    Q[r, c] = X_ip1[r, c];
            for (int c = 0; c < j; c++) Q[order, c] = Y_ii[0, c];

            Matrix<double> Theta;
            try { Theta = Q * P.Transpose() * (P * P.Transpose()).Inverse(); }
            catch { throw new Exception("State-space LS failed"); }

            var A_mat = Theta.SubMatrix(0, order, 0, order);
            var B_mat = Theta.SubMatrix(0, order, order, 1);
            var C_mat = Theta.SubMatrix(order, 1, 0, order);
            double D_sc = Theta[order, order];

            // ── 9. 모델 분석 (고유값 기반 지배 시상수, DC 이득) ──
            var eigA = A_mat.Evd();
            double dominantTau = dt;
            for (int k = 0; k < order; k++)
            {
                double eigMag = eigA.EigenValues[k].Magnitude;
                if (eigMag < 1e-10 || eigMag >= 1.0) continue;
                double tau_k = -dt / Math.Log(eigMag);
                if (tau_k > dominantTau) dominantTau = tau_k;
            }

            double dcGain;
            try
            {
                var IA = MB.DenseIdentity(order) - A_mat;
                var IA_invB = IA.Solve(B_mat);
                dcGain = D_sc;
                for (int k = 0; k < order; k++) dcGain += C_mat[0, k] * IA_invB[k, 0];
            }
            catch { dcGain = double.NaN; }

            // ── 10. PID 시뮬레이션 + PSO 탐색 ──
            // ── DC 게인 보정 ──
            // (1) 음수 DC → 양의 피드백 = PSO 전 particle 발산. B, D 부호 반전으로 해결.
            // (2) |DC| 가 매우 크거나 작으면 step to 1.0 이 비현실적.
            //     yTarget 를 |DC| 기준으로 스케일 → 달성 가능 범위 내에서 ITAE 평가.
            if (dcGain < -1e-6)
            {
                for (int k = 0; k < order; k++) B_mat[k, 0] = -B_mat[k, 0];
                D_sc = -D_sc;
                dcGain = -dcGain;
            }
            double refScale = (!double.IsNaN(dcGain) && dcGain > 1e-3)
                ? Math.Min(1.0, Math.Abs(dcGain) * 0.8)
                : 1.0;

            int simLen = Math.Min(800, Math.Max(200, (int)(6.0 * dominantTau / dt)));
            double targetTs = Math.Max(0.5, 2.0 * dominantTau);
            double tauM = 0.2 * targetTs;

            double[] yTarget = new double[simLen];
            for (int k = 0; k < simLen; k++)
            {
                double tm = k * dt / tauM;
                yTarget[k] = refScale * (1.0 - (1.0 + tm) * Math.Exp(-tm));
            }

            double[,] Ac = new double[order, order];
            double[] Bc = new double[order];
            double[] Cc = new double[order];
            for (int r = 0; r < order; r++)
            {
                for (int c = 0; c < order; c++) Ac[r, c] = A_mat[r, c];
                Bc[r] = B_mat[r, 0];
                Cc[r] = C_mat[0, r];
            }
            double Dc = D_sc;

            Func<double, double, double, double> evaluate = (tryKp, tryTi, tryTd) =>
            {
                double[] x = new double[order];
                double[] xNext = new double[order];
                double integ = 0;
                double prevE = 0;
                double yMeas = 0;   // 1-샘플 지연 측정값
                double maxY = 0;
                double cost = 0;

                for (int k = 0; k < simLen; k++)
                {
                    double r = refScale;
                    double e = r - yMeas;
                    integ += e * dt;
                    double deriv = (k > 0) ? (e - prevE) / dt : 0;
                    prevE = e;

                    double uk = tryKp * (e + integ / tryTi + tryTd * deriv);
                    if (uk > 1.0) uk = 1.0;
                    else if (uk < -1.0) uk = -1.0;

                    // y(k) = C x(k) + D u(k)
                    double yk = Dc * uk;
                    for (int i2 = 0; i2 < order; i2++) yk += Cc[i2] * x[i2];

                    // x(k+1) = A x(k) + B u(k)
                    for (int i2 = 0; i2 < order; i2++)
                    {
                        double s = Bc[i2] * uk;
                        for (int jj = 0; jj < order; jj++) s += Ac[i2, jj] * x[jj];
                        xNext[i2] = s;
                    }
                    var tmp = x; x = xNext; xNext = tmp;

                    if (double.IsNaN(yk) || double.IsInfinity(yk) || Math.Abs(yk) > 100)
                        return double.MaxValue;

                    double err = yTarget[k] - yk;
                    cost += k * dt * Math.Abs(err) * dt;   // ITAE
                    if (yk > maxY) maxY = yk;

                    yMeas = yk; // 다음 스텝에서 제어기가 참조
                }
                cost += 10.0 * Math.Max(0, maxY - refScale);    // 오버슈트 페널티
                return cost;
            };

            // PSO (log-공간 탐색)
            const int nParticles = 20;
            const int maxIter = 30;
            const double w_pso = 0.7;
            const double c1 = 1.5;
            const double c2 = 1.5;
            double[] lo = { Math.Log(0.001), Math.Log(0.1),  Math.Log(0.01) };
            double[] hi = { Math.Log(1.0),   Math.Log(250.0), Math.Log(10.0) };

            var rng = new System.Random(42);
            double[][] pos = new double[nParticles][];
            double[][] vel = new double[nParticles][];
            double[][] pBest = new double[nParticles][];
            double[] pBestCost = new double[nParticles];
            double[] gBest = new double[3];
            double gBestCost = double.MaxValue;

            for (int p = 0; p < nParticles; p++)
            {
                pos[p] = new double[3];
                vel[p] = new double[3];
                pBest[p] = new double[3];
                for (int d = 0; d < 3; d++)
                {
                    pos[p][d] = lo[d] + rng.NextDouble() * (hi[d] - lo[d]);
                    vel[p][d] = (rng.NextDouble() - 0.5) * (hi[d] - lo[d]) * 0.1;
                    pBest[p][d] = pos[p][d];
                }
                double cost = evaluate(Math.Exp(pos[p][0]), Math.Exp(pos[p][1]), Math.Exp(pos[p][2]));
                pBestCost[p] = cost;
                if (cost < gBestCost) { gBestCost = cost; Array.Copy(pos[p], gBest, 3); }
            }

            for (int it = 0; it < maxIter; it++)
            {
                for (int p = 0; p < nParticles; p++)
                {
                    for (int d = 0; d < 3; d++)
                    {
                        double r1 = rng.NextDouble();
                        double r2 = rng.NextDouble();
                        vel[p][d] = w_pso * vel[p][d]
                                  + c1 * r1 * (pBest[p][d] - pos[p][d])
                                  + c2 * r2 * (gBest[d] - pos[p][d]);
                        pos[p][d] += vel[p][d];
                        if (pos[p][d] < lo[d]) { pos[p][d] = lo[d]; vel[p][d] = 0; }
                        if (pos[p][d] > hi[d]) { pos[p][d] = hi[d]; vel[p][d] = 0; }
                    }
                    double cost = evaluate(Math.Exp(pos[p][0]), Math.Exp(pos[p][1]), Math.Exp(pos[p][2]));
                    if (cost < pBestCost[p])
                    {
                        pBestCost[p] = cost;
                        Array.Copy(pos[p], pBest[p], 3);
                        if (cost < gBestCost) { gBestCost = cost; Array.Copy(pos[p], gBest, 3); }
                    }
                }
            }

            double bestKp = Math.Round(Math.Max(0.001, Math.Min(1.0,   Math.Exp(gBest[0]))), 3);   // FTD step 0.001
            double bestTi = Math.Round(Math.Max(0.1,   Math.Min(250.0, Math.Exp(gBest[1]))), 1);   // FTD step 0.1
            double bestTd = Math.Round(Math.Max(0.0,   Math.Min(10.0,  Math.Exp(gBest[2]))), 1);   // FTD step 0.1

            string dcStr = double.IsNaN(dcGain) ? "int" : dcGain.ToString("0.00");
            string modelInfoStr = $"N4SID n={order} DC={dcStr} τp={dominantTau:0.00} D={D_sc:0.000} cost={gBestCost:0.000}";

            return new ModelPidResult { Kp = bestKp, Ti = bestTi, Td = bestTd, ModelInfo = modelInfoStr };
        }

        // ============================================================
        // PEM (PBSID-opt + Gauss-Newton 정련) → 폐루프 무편향 식별
        // CRLB 점근적 도달. 표준 산업 파이프라인 (MATLAB n4sid+ssest 구조).
        // ============================================================
        private static ModelPidResult ComputePemPid(double[] u, double[] y, double dt)
        {
            int N = Math.Min(u.Length, y.Length);
            if (N < 200) throw new Exception("Too few samples for PEM (need >= 200)");

            // ── 0. 입력 sanity check ──
            // NaN/Inf, 가진 전무(u std ≈ 0), 응답 전무(y std ≈ 0) 차단 → silent 실패 방지
            double sumU = 0, sumU2 = 0, sumY = 0, sumY2 = 0;
            for (int i = 0; i < N; i++)
            {
                if (double.IsNaN(u[i]) || double.IsInfinity(u[i])) throw new Exception("u has NaN/Inf");
                if (double.IsNaN(y[i]) || double.IsInfinity(y[i])) throw new Exception("y has NaN/Inf");
                sumU += u[i]; sumU2 += u[i] * u[i];
                sumY += y[i]; sumY2 += y[i] * y[i];
            }
            double stdU = Math.Sqrt(Math.Max(0, sumU2 / N - (sumU / N) * (sumU / N)));
            double stdY = Math.Sqrt(Math.Max(0, sumY2 / N - (sumY / N) * (sumY / N)));
            if (stdU < 1e-5) throw new Exception($"Excitation too weak (std(u)={stdU:0.0e})");
            if (stdY < 1e-5) throw new Exception($"Response too weak (std(y)={stdY:0.0e})");

            // ── 1. PBSID-opt 초기 식별 ──
            int pastP = Math.Min(30, N / 8);
            var initModel = PemIdentifier.IdentifyPbsid(u, y, pastP, orderCap: 2);

            // ── 2. PEM 정련 (LM + finite-difference Jacobian) ──
            var refined = PemIdentifier.RefinePem(initModel, u, y, maxIter: 15, tol: 1e-6);
            int n = refined.Order;
            var A = refined.A;
            var B = refined.B;
            var C = refined.C;
            double D_sc = refined.D;

            // ── 3. 모델 분석 ──
            var eigA = A.Evd();
            double dominantTau = dt;
            for (int k = 0; k < n; k++)
            {
                double mag = eigA.EigenValues[k].Magnitude;
                if (mag < 1e-10 || mag >= 1.0) continue;
                double tau_k = -dt / Math.Log(mag);
                if (tau_k > dominantTau) dominantTau = tau_k;
            }

            double dcGain;
            try
            {
                var IA = MB.DenseIdentity(n) - A;
                var IAB = IA.Solve(B);
                dcGain = D_sc;
                for (int k = 0; k < n; k++) dcGain += C[0, k] * IAB[k, 0];
            }
            catch { dcGain = double.NaN; }

            // ── DC 게인 보정 (음수 플립 + 스텝 스케일링) ──
            if (dcGain < -1e-6)
            {
                for (int k = 0; k < n; k++) B[k, 0] = -B[k, 0];
                D_sc = -D_sc;
                dcGain = -dcGain;
            }
            double refScale = (!double.IsNaN(dcGain) && dcGain > 1e-3)
                ? Math.Min(1.0, Math.Abs(dcGain) * 0.8)
                : 1.0;

            // ── 4. PSO + 폐루프 시뮬 (1-샘플 지연 측정) ──
            int simLen = Math.Min(800, Math.Max(200, (int)(6.0 * dominantTau / dt)));
            double targetTs = Math.Max(0.5, 2.0 * dominantTau);
            double tauM = 0.2 * targetTs;

            double[] yTarget = new double[simLen];
            for (int k = 0; k < simLen; k++)
            {
                double tm = k * dt / tauM;
                yTarget[k] = refScale * (1.0 - (1.0 + tm) * Math.Exp(-tm));
            }

            double[,] Ac = new double[n, n];
            double[] Bc = new double[n];
            double[] Cc = new double[n];
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++) Ac[r, c] = A[r, c];
                Bc[r] = B[r, 0];
                Cc[r] = C[0, r];
            }
            double Dc = D_sc;

            Func<double, double, double, double> evaluate = (tryKp, tryTi, tryTd) =>
            {
                double[] x = new double[n];
                double[] xNext = new double[n];
                double integ = 0, prevE = 0, yMeas = 0, maxY = 0, cost = 0;

                for (int k = 0; k < simLen; k++)
                {
                    double r = refScale;
                    double e = r - yMeas;
                    integ += e * dt;
                    double deriv = (k > 0) ? (e - prevE) / dt : 0;
                    prevE = e;

                    double uk = tryKp * (e + integ / tryTi + tryTd * deriv);
                    if (uk > 1.0) uk = 1.0;
                    else if (uk < -1.0) uk = -1.0;

                    double yk = Dc * uk;
                    for (int i2 = 0; i2 < n; i2++) yk += Cc[i2] * x[i2];

                    for (int i2 = 0; i2 < n; i2++)
                    {
                        double s = Bc[i2] * uk;
                        for (int jj = 0; jj < n; jj++) s += Ac[i2, jj] * x[jj];
                        xNext[i2] = s;
                    }
                    var tmp = x; x = xNext; xNext = tmp;

                    if (double.IsNaN(yk) || double.IsInfinity(yk) || Math.Abs(yk) > 100)
                        return double.MaxValue;

                    double err = yTarget[k] - yk;
                    cost += k * dt * Math.Abs(err) * dt;
                    if (yk > maxY) maxY = yk;
                    yMeas = yk;
                }
                cost += 10.0 * Math.Max(0, maxY - refScale);
                return cost;
            };

            const int nParticles = 20;
            const int maxIter = 30;
            const double w_pso = 0.7;
            const double c1 = 1.5;
            const double c2 = 1.5;
            double[] lo = { Math.Log(0.001), Math.Log(0.1),  Math.Log(0.01) };
            double[] hi = { Math.Log(1.0),   Math.Log(250.0), Math.Log(10.0) };

            // 멀티시드 앙상블: 3개 독립 PSO 실행 → 최저 cost 선택
            // 단일 시드(42)는 재현성 좋지만 지역해에 갇힐 수 있음.
            // 여러 시드로 초기 분포 다양화 → 전역해 도달 확률 ↑, variance 추정 가능.
            int[] seeds = { 42, 17, 83 };
            double[] globalBest = new double[3];
            double globalBestCost = double.MaxValue;

            foreach (int seed in seeds)
            {
                var rng = new System.Random(seed);
                double[][] pos = new double[nParticles][];
                double[][] vel = new double[nParticles][];
                double[][] pBest = new double[nParticles][];
                double[] pBestCost = new double[nParticles];
                double[] gBest = new double[3];
                double gBestCost = double.MaxValue;

                for (int p = 0; p < nParticles; p++)
                {
                    pos[p] = new double[3]; vel[p] = new double[3]; pBest[p] = new double[3];
                    for (int d = 0; d < 3; d++)
                    {
                        pos[p][d] = lo[d] + rng.NextDouble() * (hi[d] - lo[d]);
                        vel[p][d] = (rng.NextDouble() - 0.5) * (hi[d] - lo[d]) * 0.1;
                        pBest[p][d] = pos[p][d];
                    }
                    double cost = evaluate(Math.Exp(pos[p][0]), Math.Exp(pos[p][1]), Math.Exp(pos[p][2]));
                    pBestCost[p] = cost;
                    if (cost < gBestCost) { gBestCost = cost; Array.Copy(pos[p], gBest, 3); }
                }

                for (int it = 0; it < maxIter; it++)
                {
                    for (int p = 0; p < nParticles; p++)
                    {
                        for (int d = 0; d < 3; d++)
                        {
                            double r1 = rng.NextDouble();
                            double r2 = rng.NextDouble();
                            vel[p][d] = w_pso * vel[p][d]
                                      + c1 * r1 * (pBest[p][d] - pos[p][d])
                                      + c2 * r2 * (gBest[d] - pos[p][d]);
                            pos[p][d] += vel[p][d];
                            if (pos[p][d] < lo[d]) { pos[p][d] = lo[d]; vel[p][d] = 0; }
                            if (pos[p][d] > hi[d]) { pos[p][d] = hi[d]; vel[p][d] = 0; }
                        }
                        double cost = evaluate(Math.Exp(pos[p][0]), Math.Exp(pos[p][1]), Math.Exp(pos[p][2]));
                        if (cost < pBestCost[p])
                        {
                            pBestCost[p] = cost;
                            Array.Copy(pos[p], pBest[p], 3);
                            if (cost < gBestCost) { gBestCost = cost; Array.Copy(pos[p], gBest, 3); }
                        }
                    }
                }

                if (gBestCost < globalBestCost)
                {
                    globalBestCost = gBestCost;
                    Array.Copy(gBest, globalBest, 3);
                }
            }

            // PSO 수렴 검사: 모든 particle 이 MaxValue 반환 → 식별된 모델이 시뮬에 부적합
            // (주로 불안정한 A 또는 비현실적 DC/임펄스 → |y|>100 감지 → 모든 cost = MaxValue)
            if (globalBestCost >= 1e100 || double.IsNaN(globalBestCost) || double.IsInfinity(globalBestCost))
                throw new Exception("PEM PSO diverged (identified model unsuitable for sim)");

            double bestKp = Math.Round(Math.Max(0.001, Math.Min(1.0,   Math.Exp(globalBest[0]))), 3);
            double bestTi = Math.Round(Math.Max(0.1,   Math.Min(250.0, Math.Exp(globalBest[1]))), 1);
            double bestTd = Math.Round(Math.Max(0.0,   Math.Min(10.0,  Math.Exp(globalBest[2]))), 1);

            // 식별 신뢰도: innovation RMS / std(y). 0에 가까울수록 모델이 y를 잘 설명.
            double identRatio = (stdY > 1e-10) ? (refined.InnovationRms / stdY) : double.NaN;

            string dcStr = double.IsNaN(dcGain) ? "int" : dcGain.ToString("0.00");
            string info = $"PEM n={n} DC={dcStr} τp={dominantTau:0.00} D={D_sc:0.000} ident={identRatio:0.00} cost={globalBestCost:0.000e0}";

            return new ModelPidResult { Kp = bestKp, Ti = bestTi, Td = bestTd, ModelInfo = info, IdentRatio = identRatio };
        }

        // ============================================================
        // BLA (Best Linear Approximation) — Frequency-domain ML
        // 멀티사인 가진 + 도구변수 (r) 로 폐루프 무편향 FRF 추정.
        // 주파수 도메인에서 직접 PID 매칭 (파라미터 SS 모델 생략).
        // 참조: Pintelon-Schoukens 2012.
        // ============================================================
        private static ModelPidResult ComputeBlaPid(double[] u, double[] y, double[] r, double dt, Settings s)
        {
            int N = Math.Min(Math.Min(u.Length, y.Length), r.Length);
            if (N < 200) throw new Exception("Too few samples for BLA (need >= 200)");

            // 입력 sanity
            double sumR = 0, sumR2 = 0;
            for (int i = 0; i < N; i++) { sumR += r[i]; sumR2 += r[i] * r[i]; }
            double stdR = Math.Sqrt(Math.Max(0, sumR2 / N - (sumR / N) * (sumR / N)));
            if (stdR < 1e-5) throw new Exception($"Excitation r std too low ({stdR:0.0e})");

            // 1. 멀티사인 주파수 재구성 (settings에서, ApplyExcitation 과 동일 식)
            double fBase = Math.Max(0.01, s.ExciteFreqHz);
            double fMax = Math.Max(fBase * 2.0, s.ChirpEndHz);
            const int nComp = 12;
            double[] freqs = new double[nComp];
            for (int k = 0; k < nComp; k++)
                freqs[k] = fBase * Math.Pow(fMax / fBase, (double)k / (nComp - 1));

            // 2. Welch-평균 FRF (도구변수법, 폐루프 노이즈/편향 감소)
            //    윈도우 수는 데이터 길이로 조정: N 짧으면 M=1 폴백
            int numSeg = N < 400 ? 1 : (N < 800 ? 3 : 4);
            var frf = BlaIdentifier.EstimateFrf(u, y, r, freqs, dt, numSeg);

            // 신뢰도 점검: γ² 기반. 0.3 이상이면 "합리적", 0.5 이상 "우수"
            const double COH_MIN = 0.3;
            int validBins = 0;
            double meanCoh = 0;
            for (int k = 0; k < nComp; k++)
            {
                if (frf.Coherence[k] > COH_MIN && !double.IsNaN(frf.G[k].Magnitude) && !double.IsInfinity(frf.G[k].Magnitude))
                {
                    validBins++;
                    meanCoh += frf.Coherence[k];
                }
            }
            if (validBins < 4) throw new Exception($"BLA: only {validBins}/{nComp} bins have γ²>{COH_MIN}");
            meanCoh /= Math.Max(1, validBins);

            // 3. 참조 모델 시상수
            double fBand = Math.Sqrt(fBase * fMax);
            double tauM = 1.0 / (2 * Math.PI * fBand * 2.0);

            // 4. PSO + 주파수 도메인 비용 (coherence 가중)
            //    cost = Σ_k γ²_k · |T_pred(jω_k) - M(jω_k)|²
            //    신뢰도 낮은 bin 영향 자동 감소
            Func<double, double, double, double> evaluate = (tryKp, tryTi, tryTd) =>
            {
                double cost = 0;
                double totalWeight = 0;
                for (int k = 0; k < nComp; k++)
                {
                    if (frf.Coherence[k] < COH_MIN) continue;
                    if (double.IsNaN(frf.G[k].Magnitude) || double.IsInfinity(frf.G[k].Magnitude)) continue;

                    double omega = 2 * Math.PI * freqs[k];
                    var Kctrl = BlaIdentifier.PidFreqResponseRad(tryKp, tryTi, tryTd, omega);
                    var GK = frf.G[k] * Kctrl;
                    var T_pred = GK / (Complex.One + GK);
                    var M = BlaIdentifier.ReferenceModel(omega, tauM, dt);
                    var err = T_pred - M;
                    double w_k = frf.Coherence[k]; // γ² ∈ (COH_MIN, 1]
                    cost += w_k * (err.Real * err.Real + err.Imaginary * err.Imaginary);
                    totalWeight += w_k;
                }
                if (totalWeight < 1e-10) return double.MaxValue;
                return cost / totalWeight;
            };

            // PSO (같은 구조: 멀티시드, log-공간 탐색)
            const int nParticles = 20;
            const int maxIter = 30;
            const double w_pso = 0.7;
            const double c1 = 1.5;
            const double c2 = 1.5;
            double[] lo = { Math.Log(0.001), Math.Log(0.1),  Math.Log(0.01) };
            double[] hi = { Math.Log(1.0),   Math.Log(250.0), Math.Log(10.0) };

            int[] seeds = { 42, 17, 83 };
            double[] gBestG = new double[3];
            double gBestCostG = double.MaxValue;

            foreach (int seed in seeds)
            {
                var rng = new System.Random(seed);
                double[][] pos = new double[nParticles][];
                double[][] vel = new double[nParticles][];
                double[][] pBest = new double[nParticles][];
                double[] pBestCost = new double[nParticles];
                double[] gBest = new double[3];
                double gBestCost = double.MaxValue;

                for (int p = 0; p < nParticles; p++)
                {
                    pos[p] = new double[3]; vel[p] = new double[3]; pBest[p] = new double[3];
                    for (int d = 0; d < 3; d++)
                    {
                        pos[p][d] = lo[d] + rng.NextDouble() * (hi[d] - lo[d]);
                        vel[p][d] = (rng.NextDouble() - 0.5) * (hi[d] - lo[d]) * 0.1;
                        pBest[p][d] = pos[p][d];
                    }
                    double cost = evaluate(Math.Exp(pos[p][0]), Math.Exp(pos[p][1]), Math.Exp(pos[p][2]));
                    pBestCost[p] = cost;
                    if (cost < gBestCost) { gBestCost = cost; Array.Copy(pos[p], gBest, 3); }
                }

                for (int it = 0; it < maxIter; it++)
                {
                    for (int p = 0; p < nParticles; p++)
                    {
                        for (int d = 0; d < 3; d++)
                        {
                            double r1 = rng.NextDouble();
                            double r2 = rng.NextDouble();
                            vel[p][d] = w_pso * vel[p][d]
                                      + c1 * r1 * (pBest[p][d] - pos[p][d])
                                      + c2 * r2 * (gBest[d] - pos[p][d]);
                            pos[p][d] += vel[p][d];
                            if (pos[p][d] < lo[d]) { pos[p][d] = lo[d]; vel[p][d] = 0; }
                            if (pos[p][d] > hi[d]) { pos[p][d] = hi[d]; vel[p][d] = 0; }
                        }
                        double cost = evaluate(Math.Exp(pos[p][0]), Math.Exp(pos[p][1]), Math.Exp(pos[p][2]));
                        if (cost < pBestCost[p])
                        {
                            pBestCost[p] = cost;
                            Array.Copy(pos[p], pBest[p], 3);
                            if (cost < gBestCost) { gBestCost = cost; Array.Copy(pos[p], gBest, 3); }
                        }
                    }
                }

                if (gBestCost < gBestCostG)
                {
                    gBestCostG = gBestCost;
                    Array.Copy(gBest, gBestG, 3);
                }
            }

            if (gBestCostG >= 1e100 || double.IsNaN(gBestCostG) || double.IsInfinity(gBestCostG))
                throw new Exception("BLA PSO diverged");

            double bestKp = Math.Round(Math.Max(0.001, Math.Min(1.0,   Math.Exp(gBestG[0]))), 3);
            double bestTi = Math.Round(Math.Max(0.1,   Math.Min(250.0, Math.Exp(gBestG[1]))), 1);
            double bestTd = Math.Round(Math.Max(0.0,   Math.Min(10.0,  Math.Exp(gBestG[2]))), 1);

            // BLA 신뢰도: 평균 γ² (1=완전 선형 종속, 0=무상관). 1-γ² 를 IdentRatio 로 변환.
            string info = $"BLA seg={frf.NumSegments}×{frf.SegmentLength} bins={validBins}/{nComp} γ²={meanCoh:0.00} τM={tauM:0.00} cost={gBestCostG:0.000e0}";
            return new ModelPidResult
            {
                Kp = bestKp, Ti = bestTi, Td = bestTd,
                ModelInfo = info,
                IdentRatio = 1.0 - meanCoh  // 낮을수록 신뢰 (γ² 높음)
            };
        }

        // ============================================================
        // UI helpers (FTD 패턴: new SubjectiveFloatClampedWithBar + M.m)
        // ============================================================

        private SubjectiveButton<VariableControllerMaster> MakeButton(string label, string tip, Action<VariableControllerMaster> onClick)
        {
            return new SubjectiveButton<VariableControllerMaster>(
                this._focus,
                M.m<VariableControllerMaster>(_ => label),
                M.m<VariableControllerMaster>(new ToolTip(tip, 260f)),
                null,
                onClick
            );
        }

        private SubjectiveToggle<VariableControllerMaster> MakeToggle(string label, string tip, Func<bool> getter, Action<bool> setter, string tag = null)
        {
            return new SubjectiveToggle<VariableControllerMaster>(
                this._focus,
                M.m<VariableControllerMaster>(_ => label),
                M.m<VariableControllerMaster>(new ToolTip(tip, 260f)),
                (VariableControllerMaster _, bool b) => setter(b),
                null,
                (VariableControllerMaster _) => getter(),
                tag == null ? Array.Empty<string>() : new[] { tag }
            );
        }

        private SubjectiveButton<VariableControllerMaster> MakeCycleButton(string title, string tip, Func<string> valueText, Action onClick, string tag = null)
        {
            return new SubjectiveButton<VariableControllerMaster>(
                this._focus,
                M.m<VariableControllerMaster>(_ => $"{title}: {valueText()} (click/클릭)"),
                M.m<VariableControllerMaster>(new ToolTip(tip, 260f)),
                null,
                _ => onClick()
            );
        }

        private SubjectiveFloatClampedWithBar<VariableControllerMaster> MakeSliderFloat(
            string titleKo,
            string tipKo,
            Func<float> getter,
            Action<float> setter,
            float min,
            float max,
            float step,
            string format,
            string tag = null)
        {
            return new SubjectiveFloatClampedWithBar<VariableControllerMaster>(
                M.m<VariableControllerMaster>(_ => min),
                M.m<VariableControllerMaster>(_ => max),
                M.m<VariableControllerMaster>(_ => getter()),
                M.m<VariableControllerMaster>(_ => step),
                this._focus,
                M.m<VariableControllerMaster>(_ => $"{titleKo}: {getter().ToString(format)}"),
                (VariableControllerMaster _, float f) => setter(f),
                (VariableControllerMaster _, float f) => $"Set to {f.ToString(format)} / {titleKo} → {f.ToString(format)}",
                M.m<VariableControllerMaster>(new ToolTip(tipKo, 260f)),
                tag == null ? Array.Empty<string>() : new[] { tag }
            );
        }

        private SubjectiveFloatClampedWithBar<VariableControllerMaster> MakeSliderInt(
            string titleKo,
            string tipKo,
            Func<int> getter,
            Action<int> setter,
            int min,
            int max,
            int step,
            string format,
            string tag = null)
        {
            return new SubjectiveFloatClampedWithBar<VariableControllerMaster>(
                M.m<VariableControllerMaster>(_ => min),
                M.m<VariableControllerMaster>(_ => max),
                M.m<VariableControllerMaster>(_ => getter()),
                M.m<VariableControllerMaster>(_ => step),
                this._focus,
                M.m<VariableControllerMaster>(_ => $"{titleKo}: {getter().ToString(format)}"),
                (VariableControllerMaster _, float f) => setter((int)Math.Round(f)),
                (VariableControllerMaster _, float f) => $"Set to {(int)Math.Round(f)} / {titleKo} → {(int)Math.Round(f)}",
                M.m<VariableControllerMaster>(new ToolTip(tipKo, 260f)),
                tag == null ? Array.Empty<string>() : new[] { tag }
            );
        }

        // ============================================================
        // small utils
        // ============================================================

        private static string WaveToKo(WaveType w)
        {
            switch (w)
            {
                case WaveType.Off: return "Off";
                case WaveType.Sine: return "Sine";
                case WaveType.Chirp: return "Chirp";
                case WaveType.MultiSine: return "MultiSine";
                default: return w.ToString();
            }
        }

        private static float Clamp(float v, float lo, float hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        private static int ClampInt(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        private static double RoundToStep(double v, double step)
        {
            if (step <= 0) return v;
            return Math.Round(v / step) * step;
        }

        private static int NextPow2(int n)
        {
            int v = 1;
            while (v < n) v <<= 1;
            return v;
        }

        /// <summary>DC + 선형 추세 제거 (in-place). spectral leakage 저감.</summary>
        private static void Detrend(double[] x)
        {
            int N = x.Length;
            if (N < 2) return;
            double sumT = 0, sumTT = 0, sumX = 0, sumXT = 0;
            for (int i = 0; i < N; i++)
            {
                double t = i;
                sumT += t;
                sumTT += t * t;
                sumX += x[i];
                sumXT += x[i] * t;
            }
            double meanT = sumT / N;
            double meanX = sumX / N;
            double den = sumTT - N * meanT * meanT;
            double slope = Math.Abs(den) < 1e-15 ? 0.0 : (sumXT - N * meanT * meanX) / den;
            double intercept = meanX - slope * meanT;
            for (int i = 0; i < N; i++)
                x[i] -= (slope * i + intercept);
        }

        /// <summary>
        /// BlockStarts 중 "가장 길면서 y 변동이 충분한" 블록을 반환.
        /// y 변동이 충분한 블록이 없으면 가장 긴 블록을 fallback으로 반환.
        /// </summary>
        private static (int start, int count) PickLongestBlock(
            List<int> blockStarts, int totalSamples,
            List<double> yData = null, double minYStd = 1e-4)
        {
            int bestStart = 0, bestLen = 0;           // 품질 조건 통과한 최장
            int fallbackStart = 0, fallbackLen = 0;   // 무조건 최장 (fallback)

            for (int i = 0; i < blockStarts.Count; i++)
            {
                int s = blockStarts[i];
                int e = (i + 1 < blockStarts.Count) ? blockStarts[i + 1] : totalSamples;
                int len = e - s;

                // 무조건 최장 갱신
                if (len > fallbackLen)
                {
                    fallbackLen = len;
                    fallbackStart = s;
                }

                // y 데이터가 있으면 품질 체크
                if (yData != null && len >= 64)
                {
                    // 이 블록의 y 표준편차 계산
                    int actualEnd = Math.Min(e, yData.Count);
                    int actualLen = actualEnd - s;
                    if (actualLen < 64) continue;

                    double sum = 0, sumSq = 0;
                    for (int j = s; j < actualEnd; j++)
                    {
                        sum += yData[j];
                        sumSq += yData[j] * yData[j];
                    }
                    double mean = sum / actualLen;
                    double var = (sumSq / actualLen) - mean * mean;
                    double std = var > 0 ? Math.Sqrt(var) : 0;

                    if (std >= minYStd && len > bestLen)
                    {
                        bestLen = len;
                        bestStart = s;
                    }
                }
                else if (yData == null && len > bestLen)
                {
                    bestLen = len;
                    bestStart = s;
                }
            }

            // 품질 통과 블록이 있으면 그걸, 없으면 fallback
            if (bestLen >= 64)
                return (bestStart, bestLen);
            return (fallbackStart, fallbackLen);
        }

        private static double StdDev(double[] data)
        {
            if (data.Length < 2) return 0.0;
            double mean = 0;
            for (int i = 0; i < data.Length; i++)
                mean += data[i];
            mean /= data.Length;
            double sum = 0;
            for (int i = 0; i < data.Length; i++)
            {
                double d = data[i] - mean;
                sum += d * d;
            }
            return Math.Sqrt(sum / (data.Length - 1));
        }

        /// <summary>
        /// 플랜트 지연 추정: 위상 기울기 기반.
        ///
        /// H(jω) = Y/U의 위상에서 순수 지연을 추출.
        /// 순수 지연 exp(-jωτ)는 위상 = -ωτ (주파수에 비례하는 선형 위상).
        /// 저주파 영역의 위상 기울기 = -τ → τ = -dφ/dω.
        /// 저주파만 사용하여 플랜트 동특성(극점/영점)의 위상과 분리.
        /// </summary>
        private static double EstimateDelay(double[] u, double[] y, double dt)
        {
            int N = Math.Min(u.Length, y.Length);
            if (N < 16) return 0.0;

            double[] ud = new double[N]; Array.Copy(u, ud, N); Detrend(ud);
            double[] yd = new double[N]; Array.Copy(y, yd, N); Detrend(yd);

            int Nfft = NextPow2(2 * N);
            double fs = 1.0 / dt;

            Complex[] Uf = new Complex[Nfft];
            Complex[] Yf = new Complex[Nfft];
            for (int i = 0; i < N; i++)
            {
                Uf[i] = new Complex(ud[i], 0);
                Yf[i] = new Complex(yd[i], 0);
            }

            Fourier.Forward(Uf, FourierOptions.Matlab);
            Fourier.Forward(Yf, FourierOptions.Matlab);

            // H(jω) = Y/U (Wiener 정규화)
            double maxUmag2 = 0;
            for (int k = 0; k < Nfft; k++)
            {
                double m2 = Uf[k].Real * Uf[k].Real + Uf[k].Imaginary * Uf[k].Imaginary;
                if (m2 > maxUmag2) maxUmag2 = m2;
            }
            double reg = maxUmag2 * 1e-4;

            // 저주파 빈에서 위상 수집 (DC 제외, ~fs/8까지) + 위상 언래핑
            int maxBin = Math.Max(2, Nfft / 8);
            double sumWF = 0, sumWP = 0, sumWW = 0, sumW = 0;
            double prevPhase = 0;
            double cumUnwrap = 0;

            for (int k = 1; k <= maxBin; k++)
            {
                double m2 = Uf[k].Real * Uf[k].Real + Uf[k].Imaginary * Uf[k].Imaginary;
                Complex H = (Complex.Conjugate(Uf[k]) * Yf[k]) / (m2 + reg);

                double w = 2.0 * Math.PI * k * fs / Nfft; // 각주파수
                double rawPhase = Math.Atan2(H.Imaginary, H.Real); // (-π, π]

                // 위상 언래핑: 인접 빈 간 점프가 π를 넘으면 2π 보정
                if (k > 1)
                {
                    double diff = rawPhase - prevPhase;
                    if (diff > Math.PI) cumUnwrap -= 2.0 * Math.PI;
                    else if (diff < -Math.PI) cumUnwrap += 2.0 * Math.PI;
                }
                prevPhase = rawPhase;
                double phase = rawPhase + cumUnwrap;

                double weight = m2 / (m2 + reg); // SNR 기반 가중치

                // 가중 선형 회귀: phase ≈ offset + slope * w
                // slope = -τ
                sumWF += weight * w * phase;
                sumWP += weight * phase;
                sumWW += weight * w * w;
                sumW  += weight * w;
            }

            // slope = (Σw·wf - Σw·Σwp/Σ1) / (Σw·ww - (Σw)²/Σ1)
            // 간소화: 가중 최소자승으로 기울기 추출
            double denom = sumWW * sumW - sumW * sumW; // 이건 항상 0이 아님 (w가 다 다르니까)
            // 더 안정적인 형태: 직접 Σw*phase*w / Σw*w*w (절편 무시, DC 제외했으니)
            double slope = sumWW > 1e-12 ? sumWF / sumWW : 0.0;

            // τ = -slope (위상 기울기가 음수면 양의 지연)
            double tau = -slope;
            return Math.Max(0.0, Math.Min(tau, 0.2)); // FTD 환경: 순수 지연 최대 0.2초 (10틱)
        }

        private static double EstimateSettlingTime(double[] y, double dt)
        {
            int N = y.Length;
            if (N < 16) return 2.0;

            // 디트렌드(DC + 선형 추세 제거) → 드리프트에 의한 시정수 과대추정 방지
            double[] yd = new double[N]; Array.Copy(y, yd, N); Detrend(yd);

            int Nfft = NextPow2(2 * N);
            Complex[] Yc = new Complex[Nfft];
            for (int i = 0; i < N; i++)
                Yc[i] = new Complex(yd[i], 0);

            Fourier.Forward(Yc, FourierOptions.Matlab);

            // PSD → 자기상관
            Complex[] AC = new Complex[Nfft];
            for (int k = 0; k < Nfft; k++)
                AC[k] = Yc[k] * Complex.Conjugate(Yc[k]);

            Fourier.Inverse(AC, FourierOptions.Matlab);

            double peak = AC[0].Real;
            if (peak < 1e-12) return 2.0;

            // 자기상관이 피크의 5% 아래로 떨어지는 지점 → 주요 시정수
            double threshold = 0.05 * peak;
            int tauIdx = N / 4; // fallback
            for (int i = 1; i < N / 2; i++)
            {
                if (AC[i].Real < threshold)
                {
                    tauIdx = i;
                    break;
                }
            }

            // 정착시간 ≈ 4 × 시정수
            double settlingTime = 4.0 * tauIdx * dt;
            return Math.Max(0.5, settlingTime);
        }
    }
}
