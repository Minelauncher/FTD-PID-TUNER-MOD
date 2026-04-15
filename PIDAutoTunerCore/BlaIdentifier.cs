// ============================================================================
// BlaIdentifier.cs — Frequency-domain BLA (Best Linear Approximation)
//
// 이론 (Pintelon-Schoukens 2012):
//   멀티사인 가진 r 의 각 성분 주파수 f_k 에서 DFT 로 직접 FRF 추정.
//   비선형성이 있어도 BLA 는 잘 정의됨 (LTI 부분의 최선 근사).
//
// ■ 폐루프 무편향: Welch-평균 도구변수법
//   단일 realization 에서는 (Y/R)/(U/R) = Y/U 로 IV 가 무효.
//   M 개 분할 윈도우 각각에서 Y_m, U_m, R_m 계산 후:
//     G_BLA(f_k) = [Σ_m Y_m · R_m*] / [Σ_m U_m · R_m*]
//   r ⊥ noise → 분자/분모 노이즈 성분이 M→∞ 에서 0으로 평균됨 → 무편향.
//   P_yr/P_ur 형태의 교차 스펙트럼 비율 (H1-style) 과 등가.
//
// ■ 통계적 Coherence (Welch):
//   γ²(f_k) = |Σ Y·R*|² / (Σ|Y|² · Σ|R|²)  ∈ [0, 1]
//   1 = y 가 r 에 완전 선형 종속, 0 = 무상관 (노이즈/비선형 지배).
//
// ■ 윈도우: Hann, 50% 오버랩. 저주파 bin 은 윈도우보다 긴 주기라 누출 심함 — 필터됨.
//
// PID 설계: 주파수 도메인 직접 매칭 — 파라미터 모델 피팅 생략.
//   K(jω_k) = 후보 PID 의 연속 근사 (ω·dt < 0.3 에서 정확)
//   T_pred(jω_k) = G·K / (1 + G·K)
//   cost = Σ_k γ²_k · |T_pred - M(jω_k)|²  (coherence 로 신뢰 빈 가중)
//
// 참고:
//   Pintelon, R. & Schoukens, J. (2012). System Identification: A Frequency
//   Domain Approach (2nd ed). Wiley-IEEE.
//   Welch, P. (1967). The use of FFT for the estimation of power spectra.
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
            public Complex[] G = Array.Empty<Complex>();        // 플랜트 FRF (IV-평균)
            public double[] Coherence = Array.Empty<double>();  // γ² ∈ [0,1]
            public int NumSegments;                             // 평균 윈도우 수
            public int SegmentLength;
        }

        /// <summary>
        /// Welch-평균 도구변수 FRF 추정. 폐루프에서 노이즈/편향 감소.
        /// M=1 이면 단일 realization (N 짧을 때 fallback).
        /// </summary>
        public static FrfEstimate EstimateFrf(double[] u, double[] y, double[] r,
            double[] freqs, double dt, int numSegments = 4)
        {
            int K = freqs.Length;
            int N = Math.Min(Math.Min(u.Length, y.Length), r.Length);
            if (N < 100) throw new Exception($"BLA needs >= 100 samples, got {N}");

            // r 의 총 RMS — 가진 자체가 약하면 진단에 활용
            double rRms = 0;
            for (int i = 0; i < N; i++) rRms += r[i] * r[i];
            rRms = Math.Sqrt(rRms / N);
            if (rRms < 1e-6) throw new Exception("Excitation r is essentially zero");

            // Welch 세그먼트 크기: L = 2N/(M+1) (50% 오버랩 가정)
            //   segments: M 개, 시작 인덱스 = 0, L/2, L, 3L/2, ...
            int M = Math.Max(1, numSegments);
            int L = 2 * N / (M + 1);
            int step = L / 2;
            if (L < 64 || step < 1)
            {
                // 데이터 부족 → M=1 로 폴백
                M = 1; L = N; step = N;
            }

            // Hann 윈도우
            double[] win = new double[L];
            for (int n = 0; n < L; n++)
                win[n] = 0.5 * (1.0 - Math.Cos(2 * Math.PI * n / (L - 1)));

            // 누적기 (M 개 세그먼트에 걸쳐)
            var sumYR = new Complex[K];
            var sumUR = new Complex[K];
            var sumYY = new double[K];
            var sumUU = new double[K];
            var sumRR = new double[K];

            double[] uw = new double[L];
            double[] yw = new double[L];
            double[] rw = new double[L];

            int segCount = 0;
            for (int segStart = 0; segStart + L <= N; segStart += step)
            {
                // 윈도우 적용
                for (int n = 0; n < L; n++)
                {
                    uw[n] = u[segStart + n] * win[n];
                    yw[n] = y[segStart + n] * win[n];
                    rw[n] = r[segStart + n] * win[n];
                }

                // 각 멀티사인 빈에서 Goertzel
                for (int k = 0; k < K; k++)
                {
                    double wps = 2 * Math.PI * freqs[k] * dt;
                    var Ym = Goertzel(yw, wps, L);
                    var Um = Goertzel(uw, wps, L);
                    var Rm = Goertzel(rw, wps, L);

                    // 교차 스펙트럼 (IV)
                    sumYR[k] += Ym * Complex.Conjugate(Rm);
                    sumUR[k] += Um * Complex.Conjugate(Rm);
                    // 자기 스펙트럼 (coherence 용)
                    sumYY[k] += Ym.Real * Ym.Real + Ym.Imaginary * Ym.Imaginary;
                    sumUU[k] += Um.Real * Um.Real + Um.Imaginary * Um.Imaginary;
                    sumRR[k] += Rm.Real * Rm.Real + Rm.Imaginary * Rm.Imaginary;
                }
                segCount++;
                if (segCount >= M) break;
            }

            if (segCount == 0) throw new Exception("No usable segments for BLA");

            // FRF 및 coherence 계산
            var G = new Complex[K];
            var coh = new double[K];

            for (int k = 0; k < K; k++)
            {
                double absYR = sumYR[k].Magnitude;
                double absUR = sumUR[k].Magnitude;

                if (absUR < 1e-15 || sumRR[k] < 1e-15 || sumYY[k] < 1e-15)
                {
                    G[k] = Complex.Zero;
                    coh[k] = 0;
                    continue;
                }

                // G = P_yr / P_ur (IV, r 을 instrument 로 써서 노이즈 상관 제거)
                G[k] = sumYR[k] / sumUR[k];

                // Coherence γ²_yr = |P_yr|² / (P_yy · P_rr)
                double gammaSq = (absYR * absYR) / Math.Max(1e-30, sumYY[k] * sumRR[k]);
                coh[k] = Math.Max(0, Math.Min(1, gammaSq));
            }

            return new FrfEstimate
            {
                Freqs = freqs,
                G = G,
                Coherence = coh,
                NumSegments = segCount,
                SegmentLength = L
            };
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
