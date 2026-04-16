# PID 자동 튜너 — 이론 및 구현

---

## 1. 개요

이 모드는 **폐루프 무편향** PID 튜닝 파이프라인을 제공합니다:

1. **PEM (주방법)** — PBSID-opt + Gauss-Newton. 폐루프 **무편향**(consistency) + **Cramér-Rao bound** 점근적 도달. 시간 도메인 상태공간 `(A,B,C,D,K)` 추정 후 PSO 스텝 시뮬.
2. **BLA (보완)** — Welch-IV 주파수 응답 추정. **비선형 강건** + 멀티사인 직접 활용. 주파수 도메인 PSO 매칭.
3. **VRFT (안전망)** — 모델 없이 직접 회귀. 3 파라미터만 → 최저 분산. 극한 폴백.

세 방법 모두 **폐루프 데이터에서 무편향** (또는 편향 미해당). N4SID (폐루프 편향) 와 OpenLoop Step ID (PID 우회 비현실적) 는 제거됨.

**식별 체인:**
```
[Auto Tune] → 가진 데이터 수집 (u, y, r)
→ PEM (시간 도메인, CRLB)     ─┐
→ BLA (주파수 도메인, 강건)    ├→ IdentRatio 정렬 → Active + Alternative
→ VRFT (직접 회귀, 안전망)    ─┘
→ UI 에서 Swap 가능 → Apply
```

---

## 2. PID 제어기란?

PID 제어기는 **오차** (목표와 현재의 차이)를 보고 보정 출력을 냅니다.

$$e(t) = \mathrm{setpoint} - \mathrm{current\ value}$$

| 항 | 수식 | 비유 |
|------|---------|---------------|
| **P** (비례) | `K_p · e` | 목표로 조향 |
| **I** (적분) | `(K_p / T_i) ∫e dt` | "너무 오래 벗어나 있었다" |
| **D** (미분) | `K_p · T_d · de/dt` | 도착 전 브레이크 |

결합:

$$u(t) = K_p \left[ e(t) + \frac{1}{T_i} \int_0^t e(\tau)\,d\tau + T_d \frac{de(t)}{dt} \right]$$

**ISA 형식의 핵심 성질:** `K_p`가 세 항 모두에 곱해짐. `K_p`를 줄이면 P, I, D가 동시에 약해짐. 포화 해소에서 이 성질을 이용.

### FTD 값 범위

| 파라미터 | 범위 | 기본값 | 비고 |
|-----------|------|---------|------|
| `K_p` (게인) | 0 ~ 1 | 0.05 | 높을수록 반응적 |
| `T_i` (적분 시간) | 0 ~ 250 | 250 (=off) | 낮을수록 강한 적분 |
| `T_d` (미분 시간) | 0 ~ 100 | 0.3 | 높을수록 감쇠 |

---

## 3. 주파수 영역 — 왜 필요한가

### 3.1 신호는 사인파의 합

모든 신호는 다른 주파수의 사인파로 분해할 수 있습니다. 이것이 **FFT**가 하는 일.

예: 기체가 0.5Hz로 흔들리면서 0.1Hz로 천천히 드리프트 → FFT는 0.5Hz와 0.1Hz에 피크를 보여줌.

### 3.2 전달함수

시간에 따른 신호 대신, 각 주파수에 시스템이 무엇을 하는지로 기술합니다.

$$H(j\omega) = \frac{Y(j\omega)}{U(j\omega)}$$

"주파수 `ω`의 사인파를 넣으면, 출력 진폭이 `|H|`배가 되고 위상이 `∠H`만큼 이동."

### 3.3 주파수 영역의 PID

$$C(s) = K_p + \frac{K_p}{T_i s} + K_p T_d s = \underbrace{\rho_1}_{K_p} + \underbrace{\rho_2}_{K_p/T_i} \cdot \frac{1}{s} + \underbrace{\rho_3}_{K_p T_d} \cdot s$$

여기서 `s = jω` (허수 주파수 변수).

PID가 세 "기저함수"의 선형 결합이 됨. 최적 PID 찾기 = 최적 `ρ` 찾기 = **한 번에 풀 수 있는 회귀 문제**.

---

## 4. VRFT 이론

### 4.1 목표

폐루프가 **참조 모델** `M(s)`처럼 동작하길 원합니다:

$$\frac{Y(s)}{R(s)} = \frac{C(s)P(s)}{1 + C(s)P(s)} \approx M(s)$$

여기서 `P(s)`는 플랜트 (기체의 물리), `C(s)`는 PID.

보통은 `P(s)`를 먼저 알아야 합니다. **VRFT는 이걸 완전히 건너뜁니다.**

### 4.2 이상적 제어기

`P(s)`를 알 수 있다면, 완벽한 제어기는:

$$C^*(s) = \frac{1}{P(s)} \cdot \frac{M(s)}{1 - M(s)}$$

**유도:** `M = CP/(1+CP)`에서 시작. `C`에 대해 풀기:

$$M(1+CP) = CP \implies M = CP - MCP = CP(1-M) \implies C = \frac{M}{P(1-M)}$$

`P(s)`를 모르니까 직접 못 씀. 여기서 트릭이 등장.

### 4.3 가상 레퍼런스 — 핵심 통찰

시스템에서 데이터 `(u, y)`를 수집했습니다. 이제 상상: **폐루프가 정확히 `M`이었다면, 이 `y`를 만든 레퍼런스는 무엇이었을까?**

`Y = M · R`이므로:

$$r_v = M^{-1} \cdot y \quad \text{(가상 레퍼런스)}$$

대응하는 오차:

$$e_v = r_v - y = (M^{-1} - 1) \cdot y \quad \text{(가상 에러)}$$

`e_v`는 `y`와 `M`만으로 계산. `P`도 필요 없고 어떤 제어기가 데이터를 만들었는지도 불필요.

### 4.4 최적화

제어기가 `C*`라면 `u = C* · e_v`가 정확히 성립. 따라서 최소화:

$$J(\theta) = \sum_{t=1}^{N} \left[ u(t) - C(\theta) \cdot e_v(t) \right]^2$$

"`e_v`를 관측된 `u`로 가장 잘 변환하는 파라미터 `θ`를 찾아라."

### 4.5 왜 올바른 답이 나오나?

핵심 메커니즘: 실제 데이터에서 `y = P · u` (플랜트가 입력을 출력으로 매핑). 따라서:

$$e_v = (M^{-1} - 1) \cdot y = (M^{-1} - 1) \cdot P \cdot u$$

`u = C · e_v`에 대입하면 `u = C · (M⁻¹ - 1) · P · u`. 양변을 `u`로 나누면:

$$1 = C(M^{-1}-1)P$$

정리하면 `C = M/[P(1-M)]` — §4.2의 이상적 제어기 `C*`와 정확히 동일. `P`는 데이터에 "인코딩"되어 있어 회귀가 `P`를 명시적으로 식별하지 않고 `C*`를 복원.

엄밀한 증명은 `N → ∞`에서 `argmin J(θ) → C*`를 보이는 것. Campi et al. (2002) Theorem 1 참조.

### 4.6 PID의 경우 — 선형 회귀

PID 형태 `C = ρ₁ + ρ₂/s + ρ₃s` 대입:

$$J = \sum_t \left[ u(t) - \rho_1 \cdot e_v(t) - \rho_2 \cdot \int e_v(t) - \rho_3 \cdot \dot{e}_v(t) \right]^2$$

`ρ`에 대해 **선형**. 정의:

$$\text{target: } b = u, \quad \text{regressors: } \phi_1 = e_v, \quad \phi_2 = \int e_v, \quad \phi_3 = \dot{e}_v$$

$$J = \| b - \rho_1 \phi_1 - \rho_2 \phi_2 - \rho_3 \phi_3 \|^2$$

### 4.7 선형 회귀 풀기 — 직관

`ρ₁, ρ₂, ρ₃`를 찾아서:

$$b \approx \rho_1 \phi_1 + \rho_2 \phi_2 + \rho_3 \phi_3$$

"`φ₁, φ₂, φ₃`를 어떤 비율로 섞으면 `b`에 가장 가까운가?"

**행렬 형태:** 모든 샘플을 행렬로 쌓기:

$$\underbrace{\begin{bmatrix} \phi_1(1) & \phi_2(1) & \phi_3(1) \\ \vdots & \vdots & \vdots \\ \phi_1(N) & \phi_2(N) & \phi_3(N) \end{bmatrix}}_{\Phi} \underbrace{\begin{bmatrix} \rho_1 \\ \rho_2 \\ \rho_3 \end{bmatrix}}_{\rho} \approx \underbrace{\begin{bmatrix} b(1) \\ \vdots \\ b(N) \end{bmatrix}}_{b}$$

**OLS (최소자승법) 해:**

$$\rho = (\Phi^T \Phi)^{-1} \Phi^T b$$

`Φᵀb`는 "각 `φ`가 `b`와 얼마나 상관있는가". `(ΦᵀΦ)⁻¹`은 `φ`들 사이의 상관을 보정.

### 4.7.1 QR 분해 — 실제 구현 방법

OLS 공식 `ρ = (ΦᵀΦ)⁻¹Φᵀb`를 직접 계산하면, `ΦᵀΦ`의 역행렬을 구해야 합니다. 데이터가 나쁘면 (조건수가 크면) 이 역행렬이 부정확 → 결과가 틀림.

**QR 분해**는 이 문제를 피하는 방법:

행렬 `Φ`를 두 행렬의 곱으로 분해:

$$\Phi = Q \cdot R$$

- `Q` = **직교 행렬** (열벡터들이 서로 수직이고 길이가 1)
- `R` = **상삼각 행렬** (대각선 아래가 전부 0)

직교 행렬의 핵심 성질: `QᵀQ = I` (단위행렬). 즉 `Qᵀ`를 곱하면 바로 역행렬 효과.

**풀이 과정:**

원래 문제:

$$\Phi \rho = b$$

QR 대입:

$$QR\rho = b$$

양변에 `Qᵀ` 곱하기 (`QᵀQ = I`이므로):

$$R\rho = Q^T b$$

`R`이 상삼각이라 아래에서 위로 한 줄씩 풀면 끝 (**역대입**, back-substitution). 역행렬을 구할 필요가 없음.

**왜 더 정확한가:** `ΦᵀΦ`를 만들면 조건수가 제곱됨 (`κ(ΦᵀΦ) = κ(Φ)²`). QR은 `Φ`를 직접 분해하니까 조건수가 그대로 유지. 예: `κ(Φ) = 1000`이면 OLS는 `κ = 10⁶`으로 악화되지만 QR은 `1000` 그대로.

실제 코드: `A.QR().Solve(b)` (MathNet 라이브러리)

### 4.8 컬럼 스케일링 — 왜 필요한가

세 회귀변수의 크기가 매우 다름:
- `φ₁ = e_v` : 보통 O(1)
- `φ₂ = ∫e_v` : 시간에 따라 누적 → O(10) 이상
- `φ₃ = ė_v` : 변화를 증폭 → O(50) (대략 `1/Δt`)

문제: 한 열이 50배 크면 회귀가 그 열에 집중하고 나머지를 무시.

**해결:** 각 열을 RMS로 정규화:

$$s_j = \sqrt{\frac{1}{L}\sum_{i=1}^{L} \phi_j[i]^2}, \quad \tilde{\phi}_j = \phi_j / s_j$$

정규화된 열에서 회귀 후 역스케일:

$$\rho_1 = \tilde{\rho}_1 \cdot s_b / s_1, \quad \rho_2 = \tilde{\rho}_2 \cdot s_b / s_2, \quad \rho_3 = \tilde{\rho}_3 \cdot s_b / s_3$$

(코드에서 `ρ₂`, `ρ₃`를 각각 `a_I`, `a_D`로 표기합니다.)

### 4.9 SVD와 조건수 — 데이터 품질 체크

**SVD (Singular Value Decomposition, 특이값 분해)란?**

모든 행렬 `Φ`를 세 행렬의 곱으로 분해하는 것:

$$\Phi = U \cdot S \cdot V^T$$

- `U` : 왼쪽 직교 행렬 (데이터의 "출력 방향")
- `S` : 대각 행렬 — 대각선에 **특이값** `σ₁ ≥ σ₂ ≥ σ₃` (각 방향의 "크기")
- `V` : 오른쪽 직교 행렬 (데이터의 "입력 방향")

**직관:** 데이터 점들이 3차원 공간에 타원체(럭비공 모양)로 분포해 있다고 상상. 특이값은 이 타원체의 **세 축 길이**:
- `σ₁` = 가장 긴 축 (데이터가 가장 많이 펼쳐진 방향)
- `σ₃` = 가장 짧은 축 (데이터가 가장 적게 펼쳐진 방향)

만약 `σ₃ ≈ 0`이면 타원체가 납작해서 한 방향 정보가 없음 = 그 방향의 파라미터를 구분할 수 없음.

**조건수:**

$$\kappa = \sigma_{max} / \sigma_{min} = \sigma_1 / \sigma_3$$

- `κ` 작음 (< 10³): 타원체가 동그란 편 → 모든 방향 정보 충분 → 좋은 회귀
- `κ` 큼 (> 10⁶): 타원체가 극도로 납작 → P, I, D 기여를 구분 불가 → 신뢰 불가

`κ`가 크면 **가진이 부족** — 충분히 다양한 주파수로 흔들지 않았다는 뜻. 단일 주파수에서는 적분항과 미분항이 같은 방향이 되어 타원체가 납작해짐 (§11.1에서 설명).

**실제 코드:**
```csharp
var svdA = A.Svd();
double sMax = svdA.S[0];           // σ₁ (최대)
double sMin = svdA.S[svdA.S.Count - 1]; // σ₃ (최소)
double condNum = sMax / sMin;      // κ
```

---

## 5. 프리필터 F — 왜, 어떻게

### 5.1 F 없이의 문제

F 없이는 회귀가 모든 주파수에 동일 가중. 하지만 저주파는 드리프트, 고주파는 노이즈가 지배. PID는 **중간 주파수**에서 주로 동작.

### 5.2 최적 선택

Campi et al. (2002)이 보인 최적 프리필터:

$$F(s) = M(s)(1 - M(s))W(s)$$

**왜 이 형태?**

- `M(s)`: 저역통과 (DC 통과, 고주파 억제)
- `(1-M(s))`: DC에서 0, 중간 주파수에서 피크 → **감도함수** (오차가 남는 정도). 제어기가 가장 효과적인 주파수를 강조
- `W(s)`: 추가 저역통과 → 측정 노이즈 억제

**밴드패스 결과:** `F`는 DC에서 0, 중간에서 피크, 고주파에서 감소. 회귀가 PID가 가장 중요한 곳에 집중.

> **주의:** `F(0) = 0` 은 DC 정보를 제거하므로 **`T_i` 추정의 분산이 구조적으로 큼** — §8 참조. VRFT 의 Ti 가 불안정할 때 IMC 규칙 또는 PEM/BLA 결과 사용 권장.

### 5.3 가중 필터 W

$$W(s) = \frac{\omega_W}{s + \omega_W}, \quad \omega_W = 2\pi f_W, \quad f_W = f_s / 8$$

1차 저역통과 필터. 컷오프 주파수 `f_W`에서 게인 -3dB (약 70%). `f_W` 위에서 10배 주파수당 -20dB 감쇠.

---

## 6. 참조 모델 M

### 6.1 M이 뜻하는 것

`M(s)`은 **폐루프가 어떻게 응답하길 원하는지** 기술. 스텝 입력 시:
- 얼마나 빨리 목표에 도달? → `T_s` (정착시간)
- 오버슈트? → 차수 `n_M`으로 조절
- 반응 전 지연? → `τ` (순수 지연)

### 6.2 수식

$$M(s) = \frac{e^{-\tau s}}{(1 + 0.2 T_s \cdot s)^2}$$

`M`에 지연이 없는데 실제 플랜트에 있으면, VRFT가 "즉시 반응하라"를 요구 → 물리적으로 불가능 → 비현실적 게인. `τ`를 넣으면 "`τ`초 대기 후 반응해도 됨"이라고 알려주는 것.

### 6.3 0.2는 어디서?

2차 시스템 `G(s) = 1/(1 + τ_m·s)²`의 스텝 응답이 최종값의 95%에 도달하는 시간:

$$t_{95} \approx 5 \tau_m$$

정착 시간 `T_s`이므로:

$$T_s = 5\tau_m \implies \tau_m = T_s / 5 = 0.2 \cdot T_s$$

`(1/(1+τ_m·s))²`의 스텝 응답은 `1 - (1 + t/τ_m)e^(-t/τ_m)`. 0.95로 놓고 풀면 `t ≈ 5τ_m`.

### 6.4 왜 `n_M = 2`?

FTD 기체는 **관성** (회전에 저항)과 **감쇠** (공기저항이 회전을 늦춤)를 가진 물리 객체 → 2차 시스템:

$$P(s) \approx \frac{K}{s(\tau_p s + 1)}$$

`τ_p`는 플랜트의 시정수 (§6.2의 순수 지연 `τ`와 다름).

### 6.5 τ (순수 지연)이란?

명령을 주고 **아무 반응도 없는** 시간. "느린 응답"이 아니라 `τ`초 동안 문자 그대로 무반응.

예: 명령 후 추력 생산까지 0.1초 걸리는 추력기. 그 0.1초 동안 출력이 정확히 0.

---

## 7. 계산 파이프라인

### 단계 1: 디트렌드

`u`, `y`에서 평균값(DC)과 선형 드리프트를 제거.

FFT는 신호가 무한 반복한다고 가정. 0에서 시작해서 5로 끝나면 5→0 "점프"를 보고 가짜 고주파를 만듦 (**스펙트럴 리키지**).

### 단계 2: 제로패딩 FFT

길이 `N_FFT = 2^⌈log₂(2N)⌉`이 되도록 0을 추가.

FFT 필터링은 **순환 합성곱**을 만듦 (신호가 감김). 0을 추가하면 **선형 합성곱**으로 변환 (감김 없음).

2의 거듭제곱일 때 FFT가 가장 빠름.

### 단계 3: 주파수 영역에서 M, W, F 적용

*표기: 대문자 (`U, Y, R_v, E_v`) = 주파수 영역; 소문자 (`u, y, r_v, e_v`) = 시간 영역.*

각 주파수 빈 `k`에 대해:

1. `M(jω_k)`, `W(jω_k)`, `F(jω_k) = M(1-M)W` 계산
2. 가상 레퍼런스: `R_v = Y / M`
3. 가상 에러: `E_v = R_v - Y = (1/M - 1) · Y`
4. 양측 필터링: `U_F = F · U`, `E_F = F · E_v`

주파수 영역에서 곱셈 = 시간 영역 필터링이지만 훨씬 빠름. 또한 `M⁻¹`은 **미래를 봐야 함** (비인과적), 시간 영역에서 불가능하지만 주파수 영역에서는 복소수 곱셈 하나.

### 단계 4: 역FFT → 시간 영역

`U_F`와 `E_F`를 시간 신호로 변환. 처음 `N`개 샘플만 사용 (나머지는 제로패딩 부산물).

### 단계 5: 회귀 벡터 구성

필터링된 가상 에러 `e_F`로부터:

**P항 (비례):**
$$\phi_1[i] = e_F[i]$$

**I항 (적분) — 사다리꼴 규칙:**
$$\phi_2[i] = \phi_2[i-1] + \frac{e_F[i-1] + e_F[i]}{2} \Delta t$$

*사다리꼴 규칙: 곡선 아래 면적을 사다리꼴로 근사. 직사각형보다 정확 (O(Δt²) vs O(Δt)).*

**D항 (미분) — 중심 차분:**
$$\phi_3[i] = \frac{e_F[i+1] - e_F[i-1]}{2\Delta t}$$

*중심 차분: 양쪽 점으로 기울기 추정. 한쪽 차분보다 정확.*

**목표:**
$$b[i] = u_F[i]$$

### 단계 6–9: 스케일, 검사, 풀기, 추출

6. 열을 RMS로 **스케일** (§4.8 참조)
7. 조건수 **검사** (§4.9 참조)
8. QR로 $\tilde{b} \approx \tilde{\rho}_1 \tilde{\phi}_1 + \tilde{\rho}_2 \tilde{\phi}_2 + \tilde{\rho}_3 \tilde{\phi}_3$ **풀기**
9. **추출:** `K_p = ρ₁`, `T_i = K_p / a_I`, `T_d = a_D / K_p`

---

## 8. Ti 한계

### VRFT가 Ti를 잘 못 구하는 이유

$$F(0) = M(0) \cdot (1 - M(0)) \cdot W(0) = 1 \times 0 \times W(0) = 0$$

프리필터 `F`가 **DC에서 정확히 0**. 정상상태(일정 오프셋) 정보가 회귀 시작 전에 완전히 제거.

`T_i`는 정상상태 오차 보정을 제어하는 파라미터. DC 정보 없이 `a_I`는 사실상 노이즈를 피팅.

**이것은 버그가 아니라 VRFT 설계에 내재된 것.** `M(0) = 1`은 "정상상태 오차 0"을 의미하며 좋은 것. 하지만 `1 - M(0) = 0`이 프리필터에서 DC를 제거. 둘 다 가질 수는 없음.

### 대체: IMC 규칙

VRFT의 `T_i`가 신뢰 불가일 때 (< 0.1 또는 > 100), **IMC (Internal Model Control)** 규칙을 사용:

$$T_i = \tau_m \times n_M = 0.2 T_s \times 2 = 0.4 T_s$$

**유도:** IMC에서 적분 시간 = 플랜트의 시정수. 참조모델 M의 시정수가 `τ_m = 0.2Ts`이고 2차(`nM=2`)이므로 등가 시정수는 `0.4Ts`. 출처가 명확한 이론 기반 규칙.

릴레이 피드백의 `Ti = 2.2Tu`도 대안 — DC=0 문제 없이 진동 데이터에서 직접 산출.

---

## 9. 폐루프 편향 — 왜 일부 방법이 제거되었나

### 폐루프 편향 (Closed-Loop Bias)

폐루프 데이터에서 `u` 와 측정 노이즈 `v` 가 상관됨 (제어기가 noise 에 반응 → u 에 noise 침투):

$$\hat{G}_{LS} \to G - \frac{K \cdot \sigma_v^2}{(1+GK) \cdot \sigma_u^2} \neq G$$

데이터 아무리 많아도 (N→∞) 이 편향은 사라지지 않음. **"정확히" 틀린 값에 수렴.**

### 제거된 방법들

| 방법 | 제거 이유 |
|------|----------|
| **N4SID** | 경사투영이 개루프 가정. 폐루프에서 편향 발생 → PEM/BLA 대비 이론적 열등. 코드에서 제거 (git: 50a3dd7). |
| **OpenLoop Step ID** | PID 우회 = 기체 즉시 불안정. FTD 환경에서 1초 이상 수집 비현실적. 코드에서 제거. |

### 남은 방법의 폐루프 무편향 원리

| 방법 | 편향 제거 방식 |
|------|---------------|
| **PEM** | noise 모델 `K` 가 `v` 흡수 → 플랜트 `(A,B,C,D)` 만 깨끗 추출. 혁신 형식. |
| **BLA** | 가진 `r` 을 도구변수로 사용 + Welch 평균 → `E[noise · r] = 0` 으로 상관 소거. |
| **VRFT** | 플랜트 `G` 자체를 추정 안 함 → 편향 개념 미적용. 데이터 → PID 직접 회귀. |

### 자동 선택 지표: IdentRatio

세 방법 모두 실행 후, **IdentRatio** 로 정렬하여 가장 신뢰도 높은 결과를 Active (주력), 2순위를 Alternative (대안) 으로 표시. 사용자는 Swap 버튼으로 교체 가능.

| 방법 | IdentRatio 정의 | 의미 | 범위 |
|------|----------------|------|------|
| **PEM** | `√V(θ) / std(y)` = 혁신 RMS / 출력 표준편차 | 모델이 y 를 얼마나 잘 예측하나 | 0 = 완벽 |
| **BLA** | `1 - mean(γ²)` = 1 - 평균 coherence | FRF 의 r↔y 선형 종속 강도 | 0 = 완벽 |
| **VRFT** | 고정 0.99 | 항상 최하위 (안전망) | 0.99 |

선택 규칙:
- IdentRatio < 0.5 인 후보만 참가 (VRFT 제외 — 0.99 고정이라 자동 최하위)
- PSO cost ≥ 1e100 (발산) 이면 해당 후보 탈락
- 가장 낮은 IdentRatio = Active

예: PEM IdentRatio=0.12, BLA IdentRatio=0.15 → Active=PEM, Alt=BLA.

---

## 10. PEM — 폐루프 무편향 최적 식별 (주방법)

### 10.1 왜 PEM?

§9의 N4SID는 경사투영이 **개루프** 가정에서 유도됨. 폐루프에선 `U_f` 가 과거 측정 노이즈와 상관 → 점근적으로도 편향.

**PEM (Prediction Error Method, Ljung 1999)** 는 예측 오차(혁신)를 직접 최소화:

$$\theta^* = \arg\min_\theta \; V(\theta) = \frac{1}{N} \sum_{k=1}^N e(k; \theta)^2$$

- 폐루프에서도 **점근적 무편향** (consistency 보장)
- Gaussian 혁신 가정 시 **Cramér-Rao bound 도달** (통계적으로 최적)
- 단점: 비선형 최적화 → 좋은 초기값 필요, local minima 리스크

해결: 서브스페이스 식별 (PBSID-opt) 로 먼저 초기화 후 GN 정련. 표준 산업 파이프라인 (MATLAB `n4sid` + `ssest`).

### 10.2 혁신 형식 (Innovation form)

데이터 생성 모델:

$$x(k+1) = A x(k) + B u(k) + K e(k)$$
$$y(k)   = C x(k) + D u(k) + e(k)$$

- `e(k) ~ WN(0, σ²)`: 혁신 (innovation)
- `K`: Kalman gain (노이즈가 상태를 통해 들어오는 경로)
- `(A - KC)` 항상 안정 (Kalman 예측기 성질)

**이점:**
- 프로세스 노이즈 + 측정 노이즈를 **하나의** `e(k)` 로 통합
- 예측기가 깔끔:

$$\hat x(k+1|k) = (A - KC) \hat x(k|k-1) + (B - KD) u(k) + K y(k)$$

$$\hat y(k|k-1) = C \hat x(k|k-1) + D u(k)$$

$$e(k) = y(k) - \hat y(k|k-1)$$

### 10.3 PBSID-opt 1단계: 고차 VAR 회귀

예측기 방정식을 `p` 샘플만큼 재귀 전개:

$$y(k) = D u(k) + \sum_{j=1}^p \xi_j \, u(k-j) + \sum_{j=1}^p \eta_j \, y(k-j) + e(k)$$

계수는 *예측기 Markov 파라미터*:

$$\xi_j = C \, A_K^{j-1} \, B_K, \qquad \eta_j = C \, A_K^{j-1} \, K$$

(여기 `A_K = A - KC`, `B_K = B - KD`.)

**폐루프 일관성 핵심:** 제어기가 1-tick 지연을 갖는 한 (FTD 물리 틱 기반 제어 → 항상 성립), 회귀자 `{u(k), u(k-1..p), y(k-1..p)}` 는 현재 혁신 `e(k)` 와 무상관. 따라서 OLS가 무편향.

회귀자 벡터 `z(k) = [u(k); u(k-1); ...; u(k-p); y(k-1); ...; y(k-p)]` (차원 `2p+1`), LS:

$$\hat\theta = (Z Z^T)^{-1} Z y \qquad \hat\theta = [D; \, \xi_1, ..., \xi_p; \, \eta_1, ..., \eta_p]$$

### 10.4 PBSID-opt 2단계: Markov Hankel + SVD

블록 `M_j = [\xi_j, \eta_j]` (1×2) 로부터 Hankel:

$$H = \begin{bmatrix} M_1 & M_2 & \cdots & M_{p_p} \\ M_2 & M_3 & \cdots & M_{p_p+1} \\ \vdots & & & \vdots \\ M_{p_f} & M_{p_f+1} & \cdots & M_{p_f+p_p-1} \end{bmatrix} \in \mathbb{R}^{p_f \times 2p_p}$$

구조적 분해:

$$H = \Gamma_{p_f} \cdot \Delta_{p_p}$$

- `Γ_{p_f} = [C; C A_K; ...; C A_K^{p_f-1}]`: `(A_K, C)` 확장 관측성
- `Δ_{p_p} = [[B_K, K],\; [A_K B_K, A_K K],\; ...]`: `(A_K, [B_K\ K])` 확장 제어성

**SVD + Gavish-Donoho** 로 차수 `n` 결정 (Gavish & Donoho 2014, §17 정리표 참조).

### 10.4.1 차수 상한 n ≤ 2

Gavish-Donoho 가 `n=3` 이상을 제안해도 **하드 캡 `n ≤ 2`** 적용. 이유:

1. FTD 비행 동특성은 대부분 **1~2차** (관성 + 감쇠). 3차 이상은 노이즈 피팅 (과적합).
2. n=4 에서 DC 게인이 -13 또는 +9987 로 추정된 사례 확인됨 — spurious 극점이 시뮬 발산 유발.
3. 파라미터 수: n=2 → `n² + 3n + 1 = 11`, n=4 → 29. 후자는 500샘플 데이터 대비 과잉.

Gavish-Donoho 는 `n ∈ {1, 2}` 중 자동 선택. 하한 1, 상한 2. 강축 커플링 (Pitch ↔ Forward 등) 이 있는 경우 2차 SISO 로는 잔차 구조 남을 수 있으나, MIMO 확장 전까지는 이 상한이 안전-성능 최선 균형.

$$\Gamma_{p_f} = U_n \cdot \Sigma_n^{1/2}, \qquad \Delta_{p_p} = \Sigma_n^{1/2} \cdot V_n^T$$

### 10.5 PBSID-opt 3단계: `(A, B, C, D, K)` 추출

- `C` = `Γ` 첫 행
- `A_K`: shift trick — `Γ_{\text{lower}} = \Gamma_{\text{upper}} \cdot A_K` 의 LS
- `[B_K, K]`: `Δ` 첫 두 열 (첫 Markov 블록)
- **복원**: `A = A_K + K C`, `B = B_K + K D`

### 10.6 PEM 정련: 혁신 비용

PBSID 초기값 `θ_0 = (A_0, B_0, C_0, D_0, K_0)` 에서 출발, `V(θ)` 최소화.

파라미터화: **완전 (fully parametrized)** SISO. 관측 정준형도 가능하나 *초기값 안전성* 과 *구현 단순성* 을 위해 직접 `θ = [\text{vec}(A), \text{vec}(B), \text{vec}(C), D, \text{vec}(K)]` (차원 `n² + 3n + 1`) 사용. 중복 파라미터는 LM의 정규화가 흡수.

### 10.7 Levenberg-Marquardt + FD Jacobian

**잔차** `e(k; θ) = y(k) - \hat y(k|k-1; θ)` 를 예측기 시뮬로 얻음.

**야코비안 `J[k, i] = ∂e(k)/∂θ_i`**: 유한차분

$$J[k, i] \approx \frac{e(k; \theta + h e_i) - e(k; \theta - h e_i)}{2h}$$

여기 `h = 10⁻⁵ · (1 + \|θ\|)` (스케일 적응).

**LM 업데이트**:

$$(J^T J + \lambda \cdot \text{diag}(J^T J)) \Delta\theta = -J^T e$$

- `λ` 초기값 = `10⁻³ · V(θ)`
- 새 `θ' = θ + Δθ` 에서 `V(θ') < V(θ)` 실패 시 `λ *= 10` 후 재시도 (step halving 효과)
- 최대 10회 trial → 실패면 종료

**왜 LM?** Gauss-Newton 은 이차수렴하나 `J^T J` 가 특이/병태일 때 발산. LM의 `λI` 정규화는 경사하강법에 점근하여 안정. 수렴 가까이선 자동으로 GN으로 전환.

**왜 FD?** 해석적 sensitivity 방정식은 n+1 개 파라미터 유형별로 따로 유도해야 함 (a, b, c, d, k). 구현 오류 리스크 큼. FD는 `O(n_{\text{params}} · N)` 추가 시뮬만 비용 — 우리 규모 (n≤4, N~500) 에선 한 iter 당 수십 ms.

### 10.8 안정성 제약

PEM 수렴 과정에서 `A_K = A - KC` 가 불안정해질 수 있음 → 예측기 발산 → cost 무의미.

매 LM trial 후:

$$\text{Reject if } \; \exists \lambda \in \sigma(A_K): \;|\lambda| \geq 1 - 10^{-8}$$

불안정 시 해당 step 거부, `λ *= 10` 으로 damping 증가. **이것은 *원 시스템* `A` 가 안정이어야 한다는 뜻이 아님** — 플랜트 자체가 적분 모드를 가져도 `A_K` 는 Kalman 성질에 의해 안정해야. 이 제약이 물리적 식별을 보장.

### 10.9 왜 K는 PSO 시뮬에 안 쓰이나

PSO는 **결정론적 스텝 응답** 을 추적 (→ `y_ref(k) = 2차 ref model`). `K` 는 순전히 노이즈 모델의 일부:

$$\underbrace{x(k+1) = A x(k) + B u(k)}_{\text{결정론적 (PSO 시뮬)}} + \underbrace{K e(k)}_{\text{노이즈 (PSO에 무관)}}$$

노이즈 부분을 빼면 `(A, B, C, D)` 만 남고, 이것이 진짜 플랜트 동특성. K는 *식별 단계* 에서 편향 제거에 기여했지만, *제어 설계 단계* 에선 역할 끝.

### 10.10 계산 복잡도 + 로버스트성

| 항목 | 스케일 | 우리 케이스 (n=3, N=500, p=30) |
|------|--------|-------------------------------|
| VAR 회귀 | `O(N·p²)` | ~450k ops |
| Markov Hankel SVD | `O(p³)` | ~27k ops |
| PEM 1회 iter | `O(N·n_{\text{params}}²)` | ~1.2M ops |
| PEM 15회 수렴 | `O(15·N·n_{\text{params}}²)` | ~18M ops (~50ms) |

**실패 모드:**

| 단계 | 실패 원인 | 처리 |
|------|----------|------|
| VAR LS `ZZ^T` | 가진 부족 → 특이 | 예외 → N4SID fallback |
| Hankel SVD | 모든 σ ≈ 0 | order = 1 기본값 |
| `A_K` shift LS | `Γ_u^T Γ_u` 특이 | 예외 → fallback |
| PEM 발산 | 불안정 궤적 | 모든 trial 실패 → 초기값 유지 |
| PSO NaN | 식별 모델 잘못 | `cost = MaxValue` |

### 10.11 DC 게인 보정 (음수 플립 + 스텝 스케일링)

PSO 시뮬 전 두 가지 보정 적용 (PEM, N4SID 폴백 공통):

**음수 DC 게인 플립**: 식별된 `dcGain < 0` 이면 `B, D` 부호 반전 → 양수 DC 확보. FTD 의 축별 부호 규약 (예: Roll 의 `-현재각도`) 이나 추정 오차로 DC 음수 가능. 양의 `Kp` + 음의 `G` = 양의 피드백 → 전 particle 발산. 반전으로 해소.

**스텝 스케일링 (refScale)**: `dcGain` 이 매우 크거나 작으면 단위 스텝 (y→1.0) 이 비현실적:
- `dcGain = 0.1` → `u=1` 로 달성 가능 `y_max = 0.1`. 목표 1.0 도달 불가 → 모든 PID 의 ITAE 거대.
- `dcGain = 10` → 미세 `u` 필요. 포화 불가피.

$$\text{refScale} = \min(1,\; |dcGain| \times 0.8)$$

`yTarget`, 스텝 입력 `r`, 오버슈트 임계 모두 `refScale` 로 스케일. 모델의 달성 가능 범위 안에서 ITAE 비용 계산.

---

## 11. BLA — 주파수 도메인 식별 (보완)

### 11.1 왜 BLA?

PEM 이 시간 도메인에서 파라미터 모델 `(A,B,C,D)` 를 피팅한다면, BLA (Best Linear Approximation) 는 주파수 도메인에서 **비파라미터** FRF 를 직접 측정. 서로 다른 관점 → 서로 다른 강/약점.

| PEM | BLA |
|-----|-----|
| 파라미터 모델 (차수 선택 필요) | 비파라미터 (12개 주파수 점) |
| 과적합 가능 | 과적합 불가 |
| 시뮬 가능 (스텝 응답) | 시뮬 불가 (주파수 매칭만) |
| DC 정보 있음 | DC 빈 없음 (최저 0.05Hz) |

### 11.2 BLA 정의

비선형 시스템의 **최선의 선형 근사** (Pintelon-Schoukens 2012):

$$G_{BLA}(j\omega) = \frac{S_{yu}(\omega)}{S_{uu}(\omega)} = \arg\min_G \; \mathbb{E}[\|y - g*u\|^2]$$

입력 분포 (진폭, 주파수) 에 종속 — "이 가진 영역에서의" 선형 모델.

### 11.3 Welch-IV 추정

Hann 윈도우, 50% 오버랩, M=3~4 세그먼트:

$$\hat{G}(f_k) = \frac{\sum_m Y_m(f_k) \cdot R_m^*(f_k)}{\sum_m U_m(f_k) \cdot R_m^*(f_k)}$$

- `r` 을 도구변수로 사용 → 폐루프 노이즈 편향 제거 (r ⊥ noise)
- Welch 평균으로 분산 감소
- Goertzel 알고리즘으로 12개 멀티사인 빈에서만 계산 — O(N) per bin

### 11.4 Coherence γ²

$$\gamma^2(f_k) = \frac{|\sum Y_m R_m^*|^2}{\sum |Y_m|^2 \cdot \sum |R_m|^2} \in [0, 1]$$

- 1 = y 가 r 에 완전 선형 종속 (깨끗한 FRF)
- 0 = 무상관 (노이즈/비선형 지배)
- PSO 비용에 가중 → 신뢰 빈 집중

구현에서 γ² < 0.3 인 빈은 PSO 비용에서 제외됨 (`COH_MIN = 0.3`). 이 임계값 이하에서는 FRF 추정의 노이즈가 신호보다 커 신뢰 불가.

### 11.5 주파수 도메인 PID 탐색

참조 모델 `M(jω) = 1/(1+τ_M jω)² · e^{-jωτ}` 에 폐루프 매칭:

$$\min_{K_p, T_i, T_d} \sum_k \gamma^2_k \cdot \left| \frac{G(f_k) K(f_k)}{1 + G(f_k) K(f_k)} - M(f_k) \right|^2$$

PID 주파수 응답: `K(jω) = K_p(1 + 1/(jωT_i) + jωT_d)`.

### 11.6 BLA 의 진폭 종속성

같은 시스템이라도 **가진 진폭** 에 따라 BLA 다름 — 비선형성이 있으면 서로 다른 동작 영역에서 다른 선형 근사. 포화가 가장 큰 영향. 포화 없으면 매끄러운 비선형의 차이는 10~30% 수준으로 실용적으로 수용 가능.

---

## 12. 자동 파라미터 추정

### 12.1 지연 τ — dt 고정

FTD에서 순수 지연(명령 → 첫 반응)은 게임 물리 틱 1개 = `dt` ≈ 0.02초.

$$\tau = dt \approx 0.02\mathrm{s}$$

폐루프 데이터에서 위상 기울기로 추정하면 PID의 위상 기여가 포함되어 **항상 과대추정**됨. 모델 프리 방법으로는 폐루프에서 순수 플랜트 지연만 분리 불가. `dt` 고정이 FTD 환경에서 가장 정확.

### 12.2 정착 시간 `T_s` — 파라미터 안정성

`T_s`를 0.1~1.0 스캔 (로그 스케일, 40단계). 각 지점에서 PID 산출. 인접 결과가 30% 이상 변하지 않는 최소 `T_s` 찾기:

$$\max\left(\frac{|\Delta K_p|}{|K_p|}, \frac{|\Delta T_i|}{|T_i|}, \frac{|\Delta T_d|}{|T_d|}\right) < 0.3$$

경계 근처에서 `T_s`를 살짝 바꿔도 파라미터가 크게 변동 → 수치적으로 신뢰 불가. 첫 안정점이 데이터가 지원하는 가장 빠른 응답.

30% PID 파라미터 변화는 대부분의 시스템에서 실제 제어 성능 차이가 거의 체감 불가. 이 이하면 `T_s`가 약간 변해도 "실질적으로 같은" 해. 경험적이지만 보수적 — 더 엄격한 임계값(예: 10%)은 더 큰 `T_s`(느린 응답)를 선택.

---

## 13. 가진 — 멀티사인

### 13.1 왜 가진이 필요한가

회귀 행렬 `Φ`의 세 열이 **선형 독립**이어야 함 — 서로 다른 정보를 담고 있어야.

단일 주파수에서는 `∫e_v`와 `ė_v`가 **같은 형태** (스케일만 다름) → 행렬이 거의 특이(singular) → P, I, D 분리 불가.

다양한 주파수는 적분·미분 응답을 다르게 만듦 → 선형 독립 → 신뢰할 수 있는 회귀.

### 13.2 멀티사인 수식

$$x(t) = \sum_{k=0}^{11} \frac{A}{\sqrt{12}} \sin(2\pi f_k t + \phi_k)$$

**12개 주파수, 로그 간격:**

$$f_k = 0.05 \times (2.0/0.05)^{k/11} \quad \mathrm{Hz} \quad (0.05 \sim 2 \mathrm{Hz})$$

PID는 주로 저주파에서 동작. 로그 간격이 저주파에 더 많은 성분을 배치.

### 13.3 슈뢰더 위상 — 피크 진폭 감소

$$\phi_k = -\frac{\pi k(k+1)}{12}$$

12개 사인파를 더하면 가끔 동시에 피크가 올 수 있음 → 큰 스파이크 → 포화.

이 특정 위상 배치가 피크/RMS 비(**크레스트 팩터**)를 최소화함을 증명 (Schroeder, 1970). 랜덤 위상 대비 **피크 ~40% 감소**.

직관: 다른 성분들이 다른 시간에 피크가 오도록 위상을 배치 → 에너지가 시간에 걸쳐 균등 분산.

### 13.4 왜 Chirp이 아닌가?

Chirp는 주파수를 순차적으로 스윕. 고주파에서 D항이 급변을 증폭 → 포화. 멀티사인은 모든 주파수를 동시에 → 특정 주파수에서 축적 없음.

---

## 14. 포화 처리

### 14.1 포화가 모든 걸 망치는 이유

`|u| = 1`일 때 PID는 더 내보내고 싶었지만 (예: `u = 3`) 구동기가 한계. 기록된 `u = 1`이 의도를 반영 못 함 → "작은 입력, 큰 응답" → 플랜트 게인 과대추정 → `K_p` 과소추정.

### 14.2 포화 해소 (수집 전)

1. 1초간 포화율 측정
2. 10% 이상이면: `K_p` 반감, 적분 상태 리셋, 재측정
3. 10% 미만이 될 때까지 반복. `K_p` < 0.001이면 최선의 데이터로 녹화 진행

적분은 시간에 따라 오차를 누적 (와인드업). `K_p`를 줄여도 누적된 적분이 `u`를 포화로 계속 밀 수 있음. 리셋으로 해소.

### 14.3 수집 중

- 연속 포화 ≥ 10 틱 → 블록 분할
- ≥ 3 분할 → 중지, `K_p` 반감 + 적분 리셋, 재시작

### 14.4 실시간 회피

$$\mathrm{amp\_scale} = \mathrm{clamp}\left(\frac{0.98 - |u|}{0.3},\ 0.1,\ 1.0\right)$$

`|u|`가 0.98에 접근하면 가진 진폭을 부드럽게 감소. 급격한 차단이 아닌 부드러운 감소.

---

## 15. FTD 특이사항

### 축별 SP/PV 구조

| 축 | SP | PV | VRFT 가능? |
|------|----|----|----------|
| Pitch | 목표 각도 | 현재 각도 | ✅ |
| Roll | 목표 각도 | -현재 각도 | ✅ (부호 반전) |
| Yaw | 보통 0 | 각도 오차 | ✅ (오차 = -실제) |
| Hover | 목표 고도 | 현재 고도 | ✅ |
| Forward | 0 | 거리/속도 | ✅ |
| Strafe | 0 | 횡방향 오프셋 | ✅ |

VRFT는 `u`와 `y`만 사용 — SP 구조는 무관.

### 튜닝 순서

Roll → Pitch → Yaw → Hover/Forward/Strafe. 빠른 축 먼저, 내부 축이 안정되면 커플링 감소.

### 환경

저고도에서 튜닝 (짙은 대기). 공기저항 증가 = 감쇠 증가 = 안정성 여유 증가. 짙은 공기에서 튜닝된 PID는 희박한 공기에서도 작동하지만, 반대는 불가.

---

## 16. 알려진 한계

| 한계 | 원인 | 완화 |
|-----------|-------|------------|
| **PEM local minima** | 비볼록 비용 `V(θ)` | PBSID-opt 초기화, LM 정규화, 멀티시드 PSO |
| PEM 불안정 수렴 | `A_K` 고유값 ≥ 1 | step halving + 안정성 제약 거부 |
| PEM 차수 n=2 고정 | 고차 시스템 표현 부족 가능 | FTD 동특성 대부분 1~2차라 충분 |
| **BLA DC 빈 없음** | 최저 주파수 0.05Hz | Ti 추정 불확실 → PEM 이 보완 |
| **BLA 진폭 종속** | 비선형 → 다른 진폭에서 다른 BLA | 포화 방지가 핵심, 의도 운전 영역 가진 |
| VRFT `T_i` 분산 큼 | `F(0) = 0` (DC 감쇠) | IMC 대체 또는 PEM/BLA 사용 |
| 음수 DC 게인 | 축 부호 규약 / 식별 오차 | B, D 부호 반전 + refScale (구현됨) |
| PSO 스텝 포화 | 저게인 플랜트에서 목표 도달 불가 | refScale = min(1, |dcGain|·0.8) (구현됨) |
| 가진 부족 → LS 특이 | PE 조건 미충족 | 멀티사인 + 적응형 진폭 (u+y 이중 체크) |
| 기동 시 보수적 `K_p` | 선형 가정 위반 | 안정 비행 중 튜닝 + 축 분리 모드 |
| 교차축 커플링 | SISO 모델 | 축 분리 (SP freeze) + 순차 튜닝 |
| PSO 수렴 변동 | 확률적 탐색 | 시드 고정 × 3 앙상블 |

---

## 17. 정리표

| 구성요소 | 논문 출처? | 근거 |
|-----------|-------------|-------|
| **PBSID-opt (VAR → Markov → SVD)** | ✅ | Chiuso 2007, Houtzager-van Wingerden 2009 |
| **PEM (혁신 cost, LM-FD)** | ✅ | Ljung 1999 (System ID 정전) |
| **LM + FD Jacobian** | ✅ | Levenberg 1944, Marquardt 1963 |
| **안정성 제약 (A_K Schur)** | ✅ | PEM 표준 관행 |
| **BLA (Best Linear Approximation)** | ✅ | Pintelon-Schoukens 2012 |
| **Welch-IV FRF 추정** | ✅ | Welch 1967, Pintelon 2012 |
| **Goertzel 단일 주파수 DFT** | ✅ | Goertzel 1958 |
| **Coherence γ² 가중** | ✅ | 신호처리 표준 |
| **DC 게인 부호 보정** | ⚠️ | 폐루프 식별 실무 관행 |
| **PSO refScale (포화 방지)** | ⚠️ | 엔지니어링 판단 |
| **Gavish-Donoho `ω(β)` 임계값** | ✅ | Gavish & Donoho 2014 |
| **PSO (로그 공간, 멀티시드)** | ✅ | Kennedy & Eberhart 1995 |
| **1-샘플 지연 측정 (D 처리)** | ⚠️ | 디지털 제어 표준 관행 |
| `M, W, F` VRFT 필터 | ✅ | Campi et al. 2002 |
| 가상 레퍼런스 `r_v = M⁻¹y` | ✅ | Core VRFT |
| OLS 회귀 | ✅ | Core VRFT |
| `T_i` IMC 대체 `0.4×Ts` | ⚠️ | IMC 규칙에서 유도 |
| FOPDT/IPDT + IMC-PID (Step ID) | ✅ | Rivera-Morari 1986 (IMC) |
| `τ` = dt 고정 | ❌ | FTD 물리 틱 기반 |
| `T_s` 파라미터 안정성 탐색 | ❌ | 강건 최적화 개념 |
| 멀티사인 (슈뢰더 위상) | ❌ | Schroeder 1970, 산업 표준 |
| 적응형 진폭 | ❌ | SNR 기반 엔지니어링 |
| 포화 해소 + 적분 리셋 | ❌ | ISA PID 성질 |
| 포화 회피 스케일링 | ❌ | 제약 인식 가진 |

---

## 참고문헌

**식별 (System Identification):**
- Ljung, L. (1999). *System Identification: Theory for the User* (2nd ed.). Prentice Hall. — PEM 표준 교과서.
- Chiuso, A. (2007). *The role of vector autoregressive modeling in predictor-based subspace identification*. Automatica, 43(6), 1034-1048. — PBSID-opt 정립.
- Houtzager, I., van Wingerden, J. W., & Verhaegen, M. (2009). *VARMAX-based closed-loop subspace model identification*. IEEE CDC 2009. — PBSID 구현.
- Van Overschee, P. & De Moor, B. (1994). *N4SID: Subspace algorithms for the identification of combined deterministic-stochastic systems*. Automatica, 30(1), 75-93.
- Van Overschee, P. & De Moor, B. (1996). *Subspace Identification for Linear Systems: Theory, Implementation, Applications*. Kluwer Academic Publishers.
- Gavish, M. & Donoho, D. L. (2014). *The Optimal Hard Threshold for Singular Values is `4/√3`*. IEEE Transactions on Information Theory, 60(8), 5040-5053.
- Forssell, U. & Ljung, L. (1999). *Closed-loop identification revisited*. Automatica, 35(7), 1215-1241.

**최적화:**
- Levenberg, K. (1944). *A method for the solution of certain non-linear problems in least squares*. Quarterly of Applied Mathematics, 2(2), 164-168.
- Marquardt, D. W. (1963). *An algorithm for least-squares estimation of nonlinear parameters*. SIAM Journal on Applied Mathematics, 11(2), 431-441.
- Kennedy, J. & Eberhart, R. (1995). *Particle Swarm Optimization*. Proceedings of IEEE International Conference on Neural Networks, IV, 1942-1948.

**제어:**
- Campi, M. C., Lecchini, A., & Savaresi, S. M. (2002). *Virtual reference feedback tuning: a direct method for the design of feedback controllers*. Automatica, 38(8), 1337-1346.
- Rivera, D. E., Morari, M., & Skogestad, S. (1986). *Internal model control: PID controller design*. Industrial & Engineering Chemistry Process Design and Development, 25(1), 252-265.

**가진:**
- Schroeder, M. R. (1970). *Synthesis of low-peak-factor signals and binary sequences with low autocorrelation*. IEEE Transactions on Information Theory, 16(1), 85-89.
