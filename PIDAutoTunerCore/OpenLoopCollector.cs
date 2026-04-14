// ============================================================================
// OpenLoopCollector.cs — 개루프 스텝 응답 데이터 수집용 DataCollector
//
// IAccelerationMeasurement를 구현하여 FTD의 PID를 우회하고
// 스텝 입력을 플랜트에 직접 넣어 FOPDT 모델 파라미터를 추출.
// ============================================================================

using System;
using System.Collections.Generic;
using BrilliantSkies.Core.Control.Tuning;

namespace PIDAutoTuner
{
    public class OpenLoopCollector : IAccelerationMeasurement
    {
        // 설정
        private readonly double _stepAmp;    // 스텝 크기
        private readonly double _duration;   // 총 수집 시간 (초)

        // 상태
        private double _time;
        private bool _done;
        private float _lastT = -1f; // 절대시간→dt 변환

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

        public OpenLoopCollector(double stepAmp, double duration)
        {
            _stepAmp = stepAmp;
            _duration = duration;
            _time = 0;
            _done = false;
            Y0 = double.NaN;
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
                return State.Working; // 첫 틱: 기준 시간 설정
            }

            // 초기 y 기록
            if (double.IsNaN(Y0))
                Y0 = processVariable;

            _time += actualDt;

            if (_time > _duration)
            {
                _done = true;
                controlValue = 0f;
                return State.Done;
            }

            // 스텝 입력: 전체 구간 동안 일정한 u
            double u = _stepAmp;
            u = Math.Max(-1.0, Math.Min(1.0, u));

            U.Add(u);
            Y.Add(processVariable);

            controlValue = (float)u;
            return State.Working;
        }
    }
}
