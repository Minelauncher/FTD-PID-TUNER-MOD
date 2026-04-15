// ============================================================================
// BlaIdentifier.cs — Frequency-domain BLA (Best Linear Approximation)
//
// 이론 (Pintelon-Schoukens 2012):
//   멀티사인 가진 r 의 각 성분 주파수 f_k 에서 DFT 로 직접 FRF 추정.
//   비선형성이 있어도 BLA 는 잘 정의됨 (LTI 부분의 최선 근사).
//
// 폐루프 편향 제거 (도구변수법):
//   T_yr(f_k) = Y(f_k) / R(f_k)   ← 폐루프 입출력 전달
//   T_ur(f_k) = U(f_k) / R(f_k)
//   G(f_k)    = T_yr / T_ur       ← 플랜트 무편향 (r ⊥ noise)
//
// PID 설계: 주파수 도메인 직접 매칭 — 파라미터 모델 피팅 생략, 12개 freq 점에서 직접 비교.
//   K(z_k) = 후보 PID 의 z=e^(jω_k·dt) 에서의 값
//   T_pred(z_k) = G·K / (1 + G·K)  (폐루프 예측)
//   cost = Σ_k W_k · |T_pred(z_k) - M(z_k)|²
//
// 참고:
//   Pintelon, R. & Schoukens, J. (2012). System Identification: A Frequency
//   Domain Approach (2nd ed). Wiley-IEEE.
// ============================================================================

using System;
using System.Numerics;

namespace PIDAutoTuner
{
    public static class BlaIdentifier
    {
        public class FrfEstimate
        {
            public double[] Freqs = Array.Empty<double>();      // Hz
            public Complex[] G = Array.Empty<Complex>();        // 플랜트 FRF
            public Complex[] T_yr = Array.Empty<Complex>();     // r→y (진단용)
            public Complex[] T_ur = Array.Empty<Complex>();     // r→u (진단용)
            public double[] Coherence = Array.Empty<double>();  // R/√(|U|·|Y|) 형태 신뢰도 지표
        }

        /// <summary>
        /// 멀티사인 가진 r 을 도구변수로 사용해 폐루프 무편향 FRF 추정.
        /// 각 freq 에서 G(f_k) = T_yr / T_ur, |R(f_k)| 작으면 NaN.
        /// </summary>
        public static FrfEstimate EstimateFrf(double[] u, double[] y, double[] r,
            double[] freqs, double dt)
        {
            int K = freqs.Length;
            int N = Math.Min(Math.Min(u.Length, y.Length), r.Length);
            if (N < 100) throw new Exception($"BLA needs >= 100 samples, got {N}");

            var U = new Complex[K];
            var Y = new Complex[K];
            var R = new Complex[K];
            var G = new Complex[K];
            var Tyr = new Complex[K];
            var Tur = new Complex[K];
            var coh = new double[K];

            // r 의 RMS — 가진 자체가 약하면 진단에 활용
            double rRms = 0;
            for (int i = 0; i < N; i++) rRms += r[i] * r[i];
            rRms = Math.Sqrt(rRms / N);
            if (rRms < 1e-6) throw new Exception("Excitation r is essentially zero");

            for (int k = 0; k < K; k++)
            {
                double w_per_sample = 2 * Math.PI * freqs[k] * dt;
                U[k] = Goertzel(u, w_per_sample, N);
                Y[k] = Goertzel(y, w_per_sample, N);
                R[k] = Goertzel(r, w_per_sample, N);

                double absR = R[k].Magnitude;
                double absU = U[k].Magnitude;
                double absY = Y[k].Magnitude;

                if (absR < 1e-8 || absU < 1e-8)
                {
                    G[k] = Complex.Zero;
                    Tyr[k] = Complex.Zero;
                    Tur[k] = Complex.Zero;
                    coh[k] = 0;
                    continue;
                }

                Tyr[k] = Y[k] / R[k];
                Tur[k] = U[k] / R[k];
                G[k] = Tyr[k] / Tur[k];

                // 신뢰도: |R| 가 |U|·|Y| 에 비해 충분히 큰지. 1.0 = 강한 r 우세, 0 = r 약함
                coh[k] = absR / Math.Sqrt(absU * absY + 1e-20);
            }

            return new FrfEstimate { Freqs = freqs, G = G, T_yr = Tyr, T_ur = Tur, Coherence = coh };
        }

        /// <summary>Goertzel 알고리즘: 단일 주파수 DFT, O(N).</summary>
        /// <param name="omegaPerSample">2π·f·dt (rad/sample)</param>
        private static Complex Goertzel(double[] x, double omegaPerSample, int N)
        {
            double cosW = Math.Cos(omegaPerSample);
            double sinW = Math.Sin(omegaPerSample);
            double coeff = 2 * cosW;

            double s = 0, s_prev = 0, s_prev2 = 0;
            for (int n = 0; n < N; n++)
            {
                s = x[n] + coeff * s_prev - s_prev2;
                s_prev2 = s_prev;
                s_prev = s;
            }
            // X[k] = s_prev - s_prev2 · e^{-jω}
            double real = s_prev - s_prev2 * cosW;
            double imag = s_prev2 * sinW;
            return new Complex(real, imag);
        }

        /// <summary>이산 PID K(z) 의 z=e^{jω·dt} 에서의 값. 후방차분 미분, 전방차분 적분.</summary>
        public static Complex PidFreqResponse(double Kp, double Ti, double Td, double omegaPerSample)
        {
            // K(z) = Kp · [1 + (dt/Ti)/(1 - z^-1) + (Td/dt)·(1 - z^-1)]
            // z^-1 = e^{-jω}
            // dt 는 omegaPerSample = ω·dt 이므로 dt 별도 인자 필요. 단순화: omega·Ti, omega·Td 형태로 변환
            // 위 식을 omega/dt 가 아닌 (omega·dt)=omegaPerSample 변수로 다시 쓰면:
            //   Let q = e^{-jω·dt}.  1 - q = 1 - cos(ω·dt) + j·sin(ω·dt)
            //   K(jω) ≈ Kp · [1 + 1/(Ti·jω) + Td·jω]   (연속 근사, 저주파에서 정확)
            // 이산-연속 차이는 ω·dt << 1 일 때 무시 가능. 우리는 0.05~2 Hz, dt≈0.02s 라
            // 최대 ω·dt ≈ 0.25 rad/sample. 연속 근사 사용해도 충분.
            // 단순화/안정성 위해 연속식 사용:
            //   K(jω) = Kp · (1 + 1/(jω·Ti) + jω·Td)
            // omegaPerSample = ω·dt → ω = omegaPerSample/dt 가 필요한데 호출자가 dt 모름.
            // 대신 ω 를 인자로 직접 받게 분리:
            throw new Exception("Use PidFreqResponseRad");
        }

        /// <summary>이산 PID K(z) 의 ω (rad/s) 에서의 주파수 응답.</summary>
        public static Complex PidFreqResponseRad(double Kp, double Ti, double Td, double omega)
        {
            // 연속 PID (이산화 차이 무시 — ω·dt << 1 영역)
            // K(jω) = Kp · (1 + 1/(jω·Ti) + jω·Td)
            if (omega < 1e-12)
                return new Complex(double.PositiveInfinity, 0); // DC 적분기 발산
            // 1/(jω·Ti) = -j/(ω·Ti)
            double real = Kp;
            double imag = Kp * (omega * Td - 1.0 / (omega * Ti));
            return new Complex(real, imag);
        }

        /// <summary>참조 모델 M(jω) = 1/(1+τ_M·jω)² · e^{-jω·τ}.</summary>
        public static Complex ReferenceModel(double omega, double tauM, double delay)
        {
            // (1+τ·jω)^2 = 1 + 2τ·jω - τ²·ω²
            double a = 1 - tauM * tauM * omega * omega;
            double b = 2 * tauM * omega;
            double mag2 = a * a + b * b;
            // 1 / (a + jb) = (a - jb) / (a² + b²)
            Complex denomInv = new Complex(a / mag2, -b / mag2);
            // delay e^{-jω·τ}
            double phaseDelay = -omega * delay;
            Complex delayTerm = new Complex(Math.Cos(phaseDelay), Math.Sin(phaseDelay));
            return denomInv * delayTerm;
        }
    }
}
