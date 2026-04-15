// ============================================================================
// PemIdentifier.cs — PBSID-opt + PEM 폐루프 무편향 시스템 식별
//
// 이론:
//   1) PBSID-opt (Chiuso 2007, Houtzager-van Wingerden 2009)
//      - 고차 VAR 회귀 → 예측기 Markov 파라미터 → SVD → 초기 (A,B,C,D,K)
//      - 폐루프 일관성 (asymptotic unbiased)
//   2) PEM (Ljung 1999)
//      - 혁신 cost V(θ) = (1/N)Σ e(k;θ)² 최소화
//      - LM + finite-difference Jacobian
//      - Cramér-Rao bound 점근적 도달
//
// 모델: 혁신 형식 (innovation form)
//   x(k+1) = A x(k) + B u(k) + K e(k)
//   y(k)   = C x(k) + D u(k) + e(k)
//   여기서 e(k) ~ WN(0, σ²), K = Kalman gain
// ============================================================================

using System;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;

namespace PIDAutoTuner
{
    public static class PemIdentifier
    {
        private static readonly MatrixBuilder<double> MB = Matrix<double>.Build;
        private static readonly VectorBuilder<double> VB = Vector<double>.Build;

        public class Model
        {
            public int Order;
            public Matrix<double> A = MB.Dense(1, 1);
            public Matrix<double> B = MB.Dense(1, 1);
            public Matrix<double> C = MB.Dense(1, 1);
            public double D;
            public Matrix<double> K = MB.Dense(1, 1);
            public string Info = "";
        }

        // ============================================================
        // PBSID-opt: 폐루프 무편향 서브스페이스 식별
        // ============================================================
        public static Model IdentifyPbsid(double[] u, double[] y, int pastP = 30, int orderCap = 4)
        {
            int N = Math.Min(u.Length, y.Length);
            if (N < pastP * 4) throw new Exception($"Need >= {pastP * 4} samples, got {N}");

            double[] ud = (double[])u.Clone(); Detrend(ud);
            double[] yd = (double[])y.Clone(); Detrend(yd);

            int p = pastP;

            // ── 1. VAR 회귀 ──
            // y(k) = D·u(k) + Σ_{j=1}^p ξ_j·u(k-j) + Σ_{j=1}^p η_j·y(k-j) + e(k)
            //   ξ_j = C·A_K^{j-1}·B_K  (예측기 Markov, B_K=B-KD)
            //   η_j = C·A_K^{j-1}·K
            //   A_K = A-KC (Kalman 예측기, 항상 안정)
            // 폐루프 일관성: 모든 회귀자 ⊥ e(k) (제어기 1-tick 지연 가정)
            int Nd = N - p;
            int regDim = 2 * p + 1; // u(k), u(k-1..p), y(k-1..p)
            var Z = MB.Dense(regDim, Nd);
            var Yvec = VB.Dense(Nd);
            for (int k = 0; k < Nd; k++)
            {
                int t = k + p;
                Yvec[k] = yd[t];
                Z[0, k] = ud[t];
                for (int j = 1; j <= p; j++)
                {
                    Z[j, k] = ud[t - j];
                    Z[p + j, k] = yd[t - j];
                }
            }

            // LS with Tikhonov: θ = (Z Z^T + λ_t·I)^-1 Z y
            // 폐루프에서 u, y 상관 → Z Z^T 가 병태일 수 있음.
            // λ_t 를 trace(ZZ^T) 의 상대 크기로 스케일.
            Vector<double> theta;
            try
            {
                var ZZT = Z * Z.Transpose();
                double traceZZT = 0;
                for (int i = 0; i < ZZT.RowCount; i++) traceZZT += ZZT[i, i];
                double lambda_t = 1e-8 * traceZZT / ZZT.RowCount;
                for (int i = 0; i < ZZT.RowCount; i++) ZZT[i, i] += lambda_t;
                theta = ZZT.Solve(Z * Yvec);
            }
            catch { throw new Exception("VAR LS failed"); }

            double D_est = theta[0];
            double[] xi = new double[p + 1];   // ξ[1..p]
            double[] eta = new double[p + 1];  // η[1..p]
            for (int j = 1; j <= p; j++) { xi[j] = theta[j]; eta[j] = theta[p + j]; }

            // ── 2. 예측기 Markov Hankel 구성 ──
            // 블록 M_j = [ξ_j, η_j]  (1 × 2)
            // Hankel H[r, c] = M_{r+c+1}, 크기 hf × (2·hp)
            int hf = p / 2;
            int hp = p / 2;
            if (hf < 2 || hp < 2) throw new Exception("PBSID horizons too small");

            var H = MB.Dense(hf, 2 * hp);
            for (int r = 0; r < hf; r++)
                for (int c = 0; c < hp; c++)
                {
                    int idx = r + c + 1;
                    if (idx <= p)
                    {
                        H[r, 2 * c] = xi[idx];
                        H[r, 2 * c + 1] = eta[idx];
                    }
                }

            // ── 3. SVD + Gavish-Donoho 차수 결정 ──
            var svd = H.Svd();
            var sigma = svd.S;
            int sigCount = sigma.Count;
            double[] sigSorted = new double[sigCount];
            for (int k = 0; k < sigCount; k++) sigSorted[k] = sigma[k];
            Array.Sort(sigSorted);
            double sigMed = sigSorted[sigCount / 2];
            double beta = (double)Math.Min(hf, 2 * hp) / Math.Max(hf, 2 * hp);
            double omega = Math.Sqrt(2 * (beta + 1) + 8 * beta /
                              ((beta + 1) + Math.Sqrt(beta * beta + 14 * beta + 1)));
            double thresh = omega * sigMed;
            int order = 1;
            int maxOrder = Math.Min(orderCap, Math.Min(hf - 1, sigCount));
            for (int k = 0; k < maxOrder; k++)
            {
                if (sigma[k] < thresh) break;
                order = k + 1;
            }

            // ── 4. Γ, Δ 추출 ──
            // Γ = U_n · Σ_n^{1/2}  (hf × n) — observability of (A_K, C)
            // Δ = Σ_n^{1/2} · V_n^T (n × 2hp) — controllability of [B_K, K]
            double[] sqrtS = new double[order];
            for (int i = 0; i < order; i++) sqrtS[i] = Math.Sqrt(sigma[i]);
            var Gamma = MB.Dense(hf, order);
            var Delta = MB.Dense(order, 2 * hp);
            for (int i = 0; i < order; i++)
            {
                for (int r = 0; r < hf; r++) Gamma[r, i] = svd.U[r, i] * sqrtS[i];
                for (int c = 0; c < 2 * hp; c++) Delta[i, c] = svd.VT[i, c] * sqrtS[i];
            }

            // C = Γ 첫 행
            var C_mat = MB.Dense(1, order);
            for (int i = 0; i < order; i++) C_mat[0, i] = Gamma[0, i];

            // A_K 추출: Γ_lower ≈ Γ_upper · A_K (shift trick, Tikhonov 정규화)
            var Gu = Gamma.SubMatrix(0, hf - 1, 0, order);
            var Gl = Gamma.SubMatrix(1, hf - 1, 0, order);
            Matrix<double> AK;
            try
            {
                var GuTGu = Gu.Transpose() * Gu;
                double traceGG = 0;
                for (int i = 0; i < order; i++) traceGG += GuTGu[i, i];
                double lambda_s = 1e-8 * traceGG / order;
                for (int i = 0; i < order; i++) GuTGu[i, i] += lambda_s;
                AK = GuTGu.Solve(Gu.Transpose() * Gl);
            }
            catch { throw new Exception("A_K shift LS failed"); }

            // [B_K | K] = Δ 첫 두 컬럼 (블록 1)
            var BK = MB.Dense(order, 1);
            var Kmat = MB.Dense(order, 1);
            for (int i = 0; i < order; i++)
            {
                BK[i, 0] = Delta[i, 0];
                Kmat[i, 0] = Delta[i, 1];
            }

            // ── 5. (A, B) 복원: A = A_K + K·C, B = B_K + K·D ──
            var A_mat = AK + Kmat * C_mat;
            var B_mat = BK + Kmat * D_est;

            return new Model
            {
                Order = order,
                A = A_mat,
                B = B_mat,
                C = C_mat,
                D = D_est,
                K = Kmat,
                Info = $"PBSID n={order} D={D_est:0.000}"
            };
        }

        // ============================================================
        // PEM 정련 (Levenberg-Marquardt + finite-difference Jacobian)
        // 혁신 cost V(θ) = (1/N) Σ e(k;θ)²
        // ============================================================
        public static Model RefinePem(Model init, double[] u, double[] y, int maxIter = 15, double tol = 1e-6)
        {
            int n = init.Order;
            int N = Math.Min(u.Length, y.Length);

            // 디트렌드 (PBSID와 동일 전처리)
            double[] ud = (double[])u.Clone(); Detrend(ud);
            double[] yd = (double[])y.Clone(); Detrend(yd);

            // 파라미터 패킹: vec(A); vec(B); vec(C); D; vec(K)
            int nParams = n * n + 3 * n + 1;
            double[] theta = new double[nParams];
            PackTheta(init, theta, n);

            double prevCost = ComputeCostOnly(theta, n, ud, yd, N);
            double initCost = prevCost;

            // LM λ 는 iter 간 지속. 성공 시 감소(GN 방향 신뢰도 ↑), 실패 시 증가(경사하강 ↑).
            // 표준 Marquardt adaptation: 성공 /= νDown, 실패 *= νUp.
            double lambda = 1e-3;
            const double nuUp = 10.0;
            const double nuDown = 10.0;
            const double lambdaMin = 1e-10;
            const double lambdaMax = 1e10;

            for (int iter = 0; iter < maxIter; iter++)
            {
                var e0 = ComputeResiduals(theta, n, ud, yd, N);
                double cost0 = SumSquares(e0) / N;

                // FD Jacobian: 파라미터별 상대 스케일 적용
                //   h_i = 1e-5 · max(|θ_i|, 1e-3)
                // vec(A) (~O(1)) 와 D (~O(0.01)) 의 스케일 차이 보정
                var J = MB.Dense(N, nParams);
                for (int i = 0; i < nParams; i++)
                {
                    double h_i = 1e-5 * Math.Max(Math.Abs(theta[i]), 1e-3);
                    double save = theta[i];
                    theta[i] = save + h_i;
                    var eP = ComputeResiduals(theta, n, ud, yd, N);
                    theta[i] = save - h_i;
                    var eM = ComputeResiduals(theta, n, ud, yd, N);
                    theta[i] = save;
                    double inv2h = 0.5 / h_i;
                    for (int k = 0; k < N; k++) J[k, i] = (eP[k] - eM[k]) * inv2h;
                }

                // LM 정규방정식: (J^T J + λ·diag(J^T J)) Δθ = -J^T e
                var JJ = J.Transpose() * J;
                var Je = J.Transpose() * VB.DenseOfArray(e0);

                double[] thetaNew = new double[nParams];
                bool accepted = false;
                double newCost = cost0;
                for (int trial = 0; trial < 10; trial++)
                {
                    var JJlam = JJ.Clone();
                    for (int i = 0; i < nParams; i++)
                        JJlam[i, i] = JJ[i, i] + lambda * Math.Max(1e-10, JJ[i, i]);

                    Vector<double> delta;
                    try { delta = JJlam.Solve(-Je); }
                    catch { lambda = Math.Min(lambda * nuUp, lambdaMax); continue; }

                    for (int i = 0; i < nParams; i++) thetaNew[i] = theta[i] + delta[i];

                    if (!IsStable(thetaNew, n)) { lambda = Math.Min(lambda * nuUp, lambdaMax); continue; }

                    double costN = ComputeCostOnly(thetaNew, n, ud, yd, N);
                    if (costN < cost0 && !double.IsNaN(costN) && !double.IsInfinity(costN))
                    {
                        Array.Copy(thetaNew, theta, nParams);
                        newCost = costN;
                        accepted = true;
                        // 성공 → λ 감소 (다음 iter 에서 GN 방향 더 신뢰)
                        lambda = Math.Max(lambda / nuDown, lambdaMin);
                        break;
                    }
                    lambda = Math.Min(lambda * nuUp, lambdaMax);
                }
                if (!accepted) break;

                if (Math.Abs(prevCost - newCost) < tol * Math.Max(1.0, prevCost)) { prevCost = newCost; break; }
                prevCost = newCost;
            }

            var result = new Model { Order = n };
            UnpackTheta(theta, result, n);
            result.Info = $"PEM n={n} D={result.D:0.000} cost={prevCost:0.4e} (init {initCost:0.4e})";
            return result;
        }

        // ============================================================
        // 내부: 혁신 잔차 시뮬 (PEM 핵심)
        // 예측기: x̂(k+1) = (A-KC)x̂ + (B-KD)u + K·y
        //         ŷ(k)   = C x̂ + D u
        //         e(k)   = y - ŷ
        // ============================================================
        private static double[] ComputeResiduals(double[] theta, int n, double[] u, double[] y, int N)
        {
            UnpackTheta_Local(theta, n, out var A, out var B, out var C, out double D, out var K);

            // A_K = A - K·C, B_K = B - K·D (matrix · row · col)
            var AK = A.Clone();
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                    AK[r, c] -= K[r, 0] * C[0, c];
            var BK = MB.Dense(n, 1);
            for (int r = 0; r < n; r++) BK[r, 0] = B[r, 0] - K[r, 0] * D;

            double[] xhat = new double[n];
            double[] xnext = new double[n];
            double[] e = new double[N];

            for (int k = 0; k < N; k++)
            {
                double yhat = D * u[k];
                for (int i = 0; i < n; i++) yhat += C[0, i] * xhat[i];
                double ek = y[k] - yhat;
                e[k] = ek;

                // x̂(k+1) = A_K x̂ + B_K u + K y
                for (int r = 0; r < n; r++)
                {
                    double s = BK[r, 0] * u[k] + K[r, 0] * y[k];
                    for (int c = 0; c < n; c++) s += AK[r, c] * xhat[c];
                    xnext[r] = s;
                }
                var tmp = xhat; xhat = xnext; xnext = tmp;
            }
            return e;
        }

        private static double ComputeCostOnly(double[] theta, int n, double[] u, double[] y, int N)
        {
            var e = ComputeResiduals(theta, n, u, y, N);
            return SumSquares(e) / N;
        }

        // ============================================================
        // 안정성: A_K = A - K·C 의 모든 고유값 |λ| < 1
        // ============================================================
        private static bool IsStable(double[] theta, int n)
        {
            UnpackTheta_Local(theta, n, out var A, out _, out var C, out _, out var K);
            var AK = A.Clone();
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                    AK[r, c] -= K[r, 0] * C[0, c];
            try
            {
                var eig = AK.Evd();
                for (int i = 0; i < n; i++)
                    if (eig.EigenValues[i].Magnitude >= 1.0 - 1e-8) return false;
                return true;
            }
            catch { return false; }
        }

        // ============================================================
        // 파라미터 패킹/언패킹
        // 순서: vec(A) (n²), vec(B) (n), vec(C) (n), D (1), vec(K) (n)
        // ============================================================
        private static void PackTheta(Model m, double[] theta, int n)
        {
            int idx = 0;
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++) theta[idx++] = m.A[r, c];
            for (int r = 0; r < n; r++) theta[idx++] = m.B[r, 0];
            for (int c = 0; c < n; c++) theta[idx++] = m.C[0, c];
            theta[idx++] = m.D;
            for (int r = 0; r < n; r++) theta[idx++] = m.K[r, 0];
        }

        private static void UnpackTheta(double[] theta, Model m, int n)
        {
            m.A = MB.Dense(n, n);
            m.B = MB.Dense(n, 1);
            m.C = MB.Dense(1, n);
            m.K = MB.Dense(n, 1);
            int idx = 0;
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++) m.A[r, c] = theta[idx++];
            for (int r = 0; r < n; r++) m.B[r, 0] = theta[idx++];
            for (int c = 0; c < n; c++) m.C[0, c] = theta[idx++];
            m.D = theta[idx++];
            for (int r = 0; r < n; r++) m.K[r, 0] = theta[idx++];
        }

        private static void UnpackTheta_Local(double[] theta, int n,
            out Matrix<double> A, out Matrix<double> B, out Matrix<double> C, out double D, out Matrix<double> K)
        {
            A = MB.Dense(n, n);
            B = MB.Dense(n, 1);
            C = MB.Dense(1, n);
            K = MB.Dense(n, 1);
            int idx = 0;
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++) A[r, c] = theta[idx++];
            for (int r = 0; r < n; r++) B[r, 0] = theta[idx++];
            for (int c = 0; c < n; c++) C[0, c] = theta[idx++];
            D = theta[idx++];
            for (int r = 0; r < n; r++) K[r, 0] = theta[idx++];
        }

        // ============================================================
        // 유틸
        // ============================================================
        private static double SumSquares(double[] x)
        {
            double s = 0;
            for (int i = 0; i < x.Length; i++) s += x[i] * x[i];
            return s;
        }

        private static void Detrend(double[] x)
        {
            int N = x.Length;
            if (N < 2) return;
            double sumT = 0, sumTT = 0, sumX = 0, sumXT = 0;
            for (int i = 0; i < N; i++)
            {
                double t = i;
                sumT += t; sumTT += t * t;
                sumX += x[i]; sumXT += x[i] * t;
            }
            double meanT = sumT / N, meanX = sumX / N;
            double den = sumTT - N * meanT * meanT;
            double slope = Math.Abs(den) < 1e-15 ? 0 : (sumXT - N * meanT * meanX) / den;
            double inter = meanX - slope * meanT;
            for (int i = 0; i < N; i++) x[i] -= slope * i + inter;
        }
    }
}
