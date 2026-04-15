// ============================================================================
// PidPlusExciteCollector.cs — PID + 가진 방식 데이터 수집
//
// PID 출력을 그대로 통과시키되 가진 신호를 더해서 u에 주입.
// 폐루프 안정성 유지 + 가진이 u에 명확히 보임 → 좋은 SNR.
//
// 주의: GetControlValue가 State.Working을 반환하면 PID 우회.
// 그래서 우리가 직접 PID를 시뮬레이션할 수 없음.
// 대신 SetPointAdjust에 가진을 더하는 기존 방식을 더 정교화하는 식으로 동작.
// 또는 DataCollector + 외부 PID 시뮬레이션 조합 필요.
// ============================================================================

using System;
using System.Collections.Generic;
using BrilliantSkies.Core.Control.Tuning;
using BrilliantSkies.Ai.Control.Pids;

namespace PIDAutoTuner
{
    /// <summary>
    /// PID + 가진 수집기.
    /// PID는 SetPointAdjust(또는 외부 PID 객체)에서 동작시키고,
    /// GetControlValue에서 (PID 출력 + 가진)을 반환.
    /// </summary>
    public class PidPlusExciteCollector : IAccelerationMeasurement
    {
        private readonly PidStandardForm _pid;     // 외부 PID (원본 복사본)
        private readonly Func<float> _getSetpoint; // SP 공급자
        private readonly double _amplitude;
        private readonly double _maxDuration;
        private readonly double _fMin;
        private readonly double _fMax;
        private readonly int _nComp;

        private double _time;
        private bool _done;
        private float _lastT = -1f;

        public readonly List<double> U = new List<double>();        // 총 출력 (PID + 가진)
        public readonly List<double> Y = new List<double>();        // 측정값
        public readonly List<double> ExciteLog = new List<double>(); // 가진 신호만

        public List<AbstractAccelerationMeasurement.Pair> Pairs { get; } = new List<AbstractAccelerationMeasurement.Pair>();
        public int Steps => 1;
        public float MaximumPositiveRate => 0f;
        public float MaximumNegativeRate => 0f;
        public Polarity Polarity => Polarity.Positive;

        public PidPlusExciteCollector(PidStandardForm pidCopy, Func<float> getSetpoint,
            double amplitude, double maxDuration,
            double fMin = 0.05, double fMax = 2.0, int nComp = 12)
        {
            _pid = pidCopy;
            _getSetpoint = getSetpoint;
            _amplitude = amplitude;
            _maxDuration = maxDuration;
            _fMin = fMin;
            _fMax = fMax;
            _nComp = nComp;
            _time = 0;
            _done = false;
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

            // 절대 시간 → dt 변환 (FTD TimeToDeltaTime과 동일)
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

            _time += actualDt;

            if (_time > _maxDuration)
            {
                _done = true;
                controlValue = 0f;
                return State.Done;
            }

            // 1) 외부 PID로 제어 출력 계산 (FTD 원본 PID와 동일 알고리즘)
            float setpoint = _getSetpoint != null ? _getSetpoint() : 0f;
            float uPid = _pid.NewMeasurementUsingDeltaTime(setpoint, processVariable, actualDt);

            // 2) 멀티사인 가진 (Schroeder 위상)
            double excite = 0;
            double compAmp = _amplitude / Math.Sqrt(_nComp);
            for (int k = 0; k < _nComp; k++)
            {
                double fk = _fMin * Math.Pow(_fMax / _fMin, (double)k / Math.Max(1, _nComp - 1));
                double phi = -Math.PI * k * (k + 1) / _nComp;
                excite += compAmp * Math.Sin(2.0 * Math.PI * fk * _time + phi);
            }

            // 3) 합산 + 클램핑
            double uTotal = (double)uPid + excite;
            uTotal = Math.Max(-1.0, Math.Min(1.0, uTotal));

            U.Add(uTotal);
            Y.Add(processVariable);
            ExciteLog.Add(excite);

            controlValue = (float)uTotal;
            return State.Working;
        }
    }
}
