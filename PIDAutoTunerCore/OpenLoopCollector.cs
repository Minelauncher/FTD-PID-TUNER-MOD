// ============================================================================
// OpenLoopCollector.cs — 개루프 데이터 수집용 DataCollector
//
// IAccelerationMeasurement를 구현하여 FTD의 PID를 우회하고
// 직접 제어 출력(u)을 플랜트에 넣어 순수 플랜트 응답을 수집.
// ============================================================================

using System;
using System.Collections.Generic;
using BrilliantSkies.Core.Control.Tuning;

namespace PIDAutoTuner
{
    /// <summary>
    /// 개루프 멀티사인 가진으로 데이터를 수집하는 DataCollector.
    /// VariableControllerMaster.DataCollector에 설정하면 PID를 우회하고
    /// 직접 u를 플랜트에 넣음.
    /// </summary>
    public class OpenLoopCollector : IAccelerationMeasurement
    {
        // 수집 설정
        private readonly double _amplitude;
        private readonly double _duration; // 초
        private readonly double _dt;
        private readonly double _fMin;
        private readonly double _fMax;
        private readonly int _nComp;

        // 수집 상태
        private double _time;
        private bool _done;
        private float _lastT = -1f; // 절대시간→dt 변환용

        // 수집 데이터
        public readonly List<double> U = new List<double>();
        public readonly List<double> Y = new List<double>();

        // IAccelerationMeasurement 인터페이스 구현 (FTD가 요구)
        public List<AbstractAccelerationMeasurement.Pair> Pairs { get; } = new List<AbstractAccelerationMeasurement.Pair>();
        public int Steps => 1;
        public float MaximumPositiveRate => 0f;
        public float MaximumNegativeRate => 0f;
        public Polarity Polarity => Polarity.Positive;

        public OpenLoopCollector(double amplitude, double duration, double dt, double fMin = 0.05, double fMax = 2.0, int nComp = 12)
        {
            _amplitude = amplitude;
            _duration = duration;
            _dt = dt;
            _fMin = fMin;
            _fMax = fMax;
            _nComp = nComp;
            _time = 0;
            _done = false;
        }

        public AccelerationSystemModel GetSystemModel() => null;

        public bool IsDone() => _done;

        /// <summary>
        /// FTD가 매 틱 호출. PID 대신 우리 제어값을 반환.
        /// 주의: FTD는 두 번째 인자로 절대 시간(time)을 넘김, dt가 아님.
        /// </summary>
        public State GetControlValue(float processVariable, float timeOrDt, out float controlValue)
        {
            if (_done)
            {
                controlValue = 0f;
                return State.Done;
            }

            // 절대 시간 → dt 변환 (FTD의 TimeToDeltaTime과 같은 로직)
            float actualDt;
            if (timeOrDt < 0.5f)
            {
                actualDt = timeOrDt; // 이미 dt
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
                return State.Working; // 첫 틱: 기준 시간 설정만
            }

            _time += actualDt;

            // 수집 시간 초과 → 완료
            if (_time > _duration)
            {
                _done = true;
                controlValue = 0f;
                return State.Done;
            }

            // 멀티사인 생성 (Schroeder 위상)
            double u = 0;
            double compAmp = _amplitude / Math.Sqrt(_nComp);
            for (int k = 0; k < _nComp; k++)
            {
                double fk = _fMin * Math.Pow(_fMax / _fMin, (double)k / (_nComp - 1));
                double phi = -Math.PI * k * (k + 1) / _nComp;
                u += compAmp * Math.Sin(2.0 * Math.PI * fk * _time + phi);
            }

            // 클램핑 (-1 ~ 1)
            u = Math.Max(-1.0, Math.Min(1.0, u));

            // 데이터 기록
            U.Add(u);
            Y.Add(processVariable);

            controlValue = (float)u;
            return State.Working;
        }
    }
}
