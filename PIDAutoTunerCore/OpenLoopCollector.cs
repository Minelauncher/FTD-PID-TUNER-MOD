// ============================================================================
// OpenLoopCollector.cs — 개루프 스텝 응답 데이터 수집용 DataCollector
//
// IAccelerationMeasurement를 구현하여 FTD의 PID를 우회하고
// 스텝 입력을 플랜트에 직접 넣어 FOPDT 모델 파라미터를 추출.
// y 변화율이 충분히 작아지면 정상상태로 판단하고 조기 종료.
// ============================================================================

using System;
using System.Collections.Generic;
using BrilliantSkies.Core.Control.Tuning;

namespace PIDAutoTuner
{
    public class OpenLoopCollector : IAccelerationMeasurement
    {
        // 설정
        private readonly double _stepAmp;
        private readonly double _maxDuration; // 최대 수집 시간

        // 상태
        private double _time;
        private bool _done;
        private float _lastT = -1f; // 절대시간→dt 변환

        // 정상상태 감지
        private double _prevY;
        private double _settledTime; // 변화율이 작은 상태가 지속된 시간
        private const double SettleThreshold = 0.01; // y 변화율 임계값 (단위/초)
        private const double SettleDuration = 0.5;   // 이 시간동안 변화율 < 임계값이면 정상상태
        private const double MinRunTime = 1.0;       // 최소 수집 시간 (너무 빨리 끝나는 거 방지)

        // 데이터
        public readonly List<double> U = new List<double>();
        public readonly List<double> Y = new List<double>();
        public double Y0; // 스텝 전 y 초기값

        // IAccelerationMeasurement 인터페이스 (FTD가 요구)
        public List<AbstractAccelerationMeasurement.Pair> Pairs { get; } = new List<AbstractAccelerationMeasurement.Pair>();
        public int Steps => 1;
        public float MaximumPositiveRate => 0f;
        public float MaximumNegativeRate => 0f;
        public Polarity Polarity => Polarity.Positive;

        public OpenLoopCollector(double stepAmp, double maxDuration = 10.0)
        {
            _stepAmp = stepAmp;
            _maxDuration = maxDuration;
            _time = 0;
            _done = false;
            Y0 = double.NaN;
            _prevY = double.NaN;
            _settledTime = 0;
        }

        public AccelerationSystemModel GetSystemModel() => null;
        public bool IsDone() => _done;

        public State GetControlValue(float processVariable, float timeOrDt, out float controlValue)
        {
            if (_done)
            {
                controlValue = 0f;
                return State.Done;
            }

            // 절대 시간 → dt 변환
            float actualDt;
            if (timeOrDt < 0.5f)
            {
                actualDt = timeOrDt;
            }
            else if (_lastT > 0f)
            {
                actualDt = timeOrDt - _lastT;
                _lastT = timeOrDt;
            }
            else
            {
                _lastT = timeOrDt;
                controlValue = 0f;
                return State.Working;
            }

            // 초기 y 기록
            if (double.IsNaN(Y0))
            {
                Y0 = processVariable;
                _prevY = processVariable;
            }

            _time += actualDt;

            // 최대 시간 초과 → 강제 종료
            if (_time > _maxDuration)
            {
                _done = true;
                controlValue = 0f;
                return State.Done;
            }

            // 정상상태 감지: y 변화율이 임계값 이하인 시간 누적
            if (!double.IsNaN(_prevY) && actualDt > 0)
            {
                double dydt = Math.Abs((processVariable - _prevY) / actualDt);
                if (dydt < SettleThreshold)
                {
                    _settledTime += actualDt;
                }
                else
                {
                    _settledTime = 0;
                }

                // 최소 시간 경과 + 충분히 오래 안정 → 정상상태 도달, 조기 종료
                if (_time > MinRunTime && _settledTime > SettleDuration)
                {
                    _done = true;
                    // 마지막 데이터 기록
                    U.Add(_stepAmp);
                    Y.Add(processVariable);
                    controlValue = 0f;
                    return State.Done;
                }
            }
            _prevY = processVariable;

            // 스텝 입력
            double u = Math.Max(-1.0, Math.Min(1.0, _stepAmp));

            U.Add(u);
            Y.Add(processVariable);

            controlValue = (float)u;
            return State.Working;
        }
    }
}
