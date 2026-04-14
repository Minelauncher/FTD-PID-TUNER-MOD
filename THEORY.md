# VRFT-based PID Auto Tuner — Theory & Implementation
# VRFT 기반 PID 자동 튜너 — 이론 및 구현

---

## 1. Overview / 개요

This mod provides two PID tuning methods:
1. **Relay Feedback** — Find initial PID when starting from scratch
2. **VRFT** — Fine-tune PID from closed-loop data

이 모드는 두 가지 PID 튜닝 방법을 제공합니다:
1. **릴레이 피드백** — 초기 PID가 없을 때 초기값 산출
2. **VRFT** — 폐루프 데이터에서 PID 미세조정

**Usage flow / 사용 흐름:**
```
PID unknown → [Relay Init] → initial PID → [Auto Tune (VRFT)] → optimized PID
PID already OK → [Auto Tune (VRFT)] → optimized PID
```

---

## 2. What Is a PID Controller? / PID 제어기란?

A PID controller watches the **error** (difference between where you want to be and where you are) and outputs a correction.

PID 제어기는 **오차** (목표와 현재의 차이)를 보고 보정 출력을 냅니다.

$$e(t) = \text{setpoint} - \text{current value}$$

The correction has three parts:

| Term | What it does | Formula | Analogy / 비유 |
|------|-------------|---------|---------------|
| **P** (Proportional) | Push proportional to error | $K_p \cdot e$ | Steering toward target / 목표로 조향 |
| **I** (Integral) | Push harder if error persists | $\frac{K_p}{T_i} \int e \, dt$ | "I've been off course too long" / "너무 오래 벗어나 있었다" |
| **D** (Derivative) | Slow down if approaching fast | $K_p T_d \frac{de}{dt}$ | Braking before arrival / 도착 전 브레이크 |

Combined:

$$u(t) = K_p \left[ e(t) + \frac{1}{T_i} \int_0^t e(\tau)\,d\tau + T_d \frac{de(t)}{dt} \right]$$

**Key property of ISA form / ISA 형식의 핵심 성질:** $K_p$ multiplies ALL three terms. Reducing $K_p$ weakens P, I, and D simultaneously. This is why we can reduce $K_p$ to desaturate.

$K_p$가 세 항 모두에 곱해짐. $K_p$를 줄이면 P, I, D가 동시에 약해짐. 포화 해소에서 이 성질을 이용.

### FTD Values / FTD 값 범위

| Parameter | Range | Default | Note |
|-----------|-------|---------|------|
| $K_p$ (Gain) | 0 ~ 1 | 0.05 | Higher = more responsive / 높을수록 반응적 |
| $T_i$ (Integral time) | 0 ~ 250 | 250 (=off) | Lower = stronger integral / 낮을수록 강한 적분 |
| $T_d$ (Derivative time) | 0 ~ 100 | 0.3 | Higher = more damping / 높을수록 감쇠 |

---

## 3. The Frequency Domain — Why We Need It / 주파수 영역 — 왜 필요한가

### 3.1 Signals Are Made of Sine Waves / 신호는 사인파의 합

Any signal can be decomposed into sine waves of different frequencies. This is what **FFT (Fast Fourier Transform)** does.

모든 신호는 다른 주파수의 사인파로 분해할 수 있습니다. 이것이 **FFT**가 하는 일.

Example: a vehicle wobbling at 0.5 Hz with a slow drift at 0.1 Hz → FFT shows peaks at 0.5 Hz and 0.1 Hz.

예: 기체가 0.5Hz로 흔들리면서 0.1Hz로 천천히 드리프트 → FFT는 0.5Hz와 0.1Hz에 피크를 보여줌.

### 3.2 Transfer Functions / 전달함수

Instead of tracking signals over time, we describe systems by what they do to each frequency.

시간에 따른 신호 대신, 각 주파수에 시스템이 무엇을 하는지로 기술합니다.

$$H(j\omega) = \frac{Y(j\omega)}{U(j\omega)}$$

This says: "if I put in a sine wave at frequency $\omega$, the output's amplitude gets multiplied by $|H|$ and the phase shifts by $\angle H$."

"주파수 $\omega$의 사인파를 넣으면, 출력 진폭이 $|H|$배가 되고 위상이 $\angle H$만큼 이동."

### 3.3 PID in Frequency Domain / 주파수 영역의 PID

The PID becomes:

$$C(s) = K_p + \frac{K_p}{T_i s} + K_p T_d s = \underbrace{\rho_1}_{K_p} + \underbrace{\rho_2}_{K_p/T_i} \cdot \frac{1}{s} + \underbrace{\rho_3}_{K_p T_d} \cdot s$$

where $s = j\omega$ (imaginary frequency variable).

**Why this helps / 왜 도움이 되나:** The PID is now a linear combination of three "basis functions" ($1$, $1/s$, $s$) with coefficients $\rho_1, \rho_2, \rho_3$. Finding the best PID = finding the best $\rho$ values = **a regression problem** that can be solved in one step.

PID가 세 "기저함수"의 선형 결합이 됨. 최적 PID 찾기 = 최적 $\rho$ 찾기 = **한 번에 풀 수 있는 회귀 문제**.

---

## 4. VRFT Theory / VRFT 이론

### 4.1 The Goal / 목표

We want the closed-loop to behave like a **reference model** $M(s)$:

$$\frac{Y(s)}{R(s)} = \frac{C(s)P(s)}{1 + C(s)P(s)} \approx M(s)$$

where $P(s)$ is the plant (the vehicle's physics) and $C(s)$ is our PID.

Normally you'd need to know $P(s)$ first. **VRFT skips this entirely.**

보통은 $P(s)$를 먼저 알아야 합니다. **VRFT는 이걸 완전히 건너뜁니다.**

### 4.2 The Ideal Controller / 이상적 제어기

If we *could* know $P(s)$, the perfect controller would be:

$$C^*(s) = \frac{1}{P(s)} \cdot \frac{M(s)}{1 - M(s)}$$

**Derivation / 유도:** Start from $M = CP/(1+CP)$. Solve for $C$:

$$M(1+CP) = CP \implies M = CP - MCP = CP(1-M) \implies C = \frac{M}{P(1-M)}$$

We can't use this directly because $P(s)$ is unknown. Here's the trick.

$P(s)$를 모르니까 직접 못 씀. 여기서 트릭이 등장.

### 4.3 Virtual Reference — The Key Insight / 가상 레퍼런스 — 핵심 통찰

We collected data $(u, y)$ from the running system. Now imagine: **if the closed loop were exactly $M$, what reference signal would have produced this $y$?**

시스템에서 데이터 $(u, y)$를 수집했습니다. 이제 상상: **폐루프가 정확히 $M$이었다면, 이 $y$를 만든 레퍼런스는 무엇이었을까?**

Since $Y = M \cdot R$:

$$r_v = M^{-1} \cdot y \quad \text{(virtual reference / 가상 레퍼런스)}$$

And the corresponding error:

$$e_v = r_v - y = (M^{-1} - 1) \cdot y \quad \text{(virtual error / 가상 에러)}$$

**The magic / 마법:** $e_v$ is computed from $y$ and $M$ only. We don't need $P$, and we don't need to know which controller generated the data.

$e_v$는 $y$와 $M$만으로 계산. $P$도 필요 없고 어떤 제어기가 데이터를 만들었는지도 불필요.

### 4.4 The Optimization / 최적화

If the controller were $C^*$, then $u = C^* \cdot e_v$ would hold exactly. So we minimize:

$$J(\theta) = \sum_{t=1}^{N} \left[ u(t) - C(\theta) \cdot e_v(t) \right]^2$$

"Find the controller parameters $\theta$ that best turn $e_v$ into the observed $u$."

"$e_v$를 관측된 $u$로 가장 잘 변환하는 파라미터 $\theta$를 찾아라."

### 4.5 Why Does This Give the Right Answer? / 왜 올바른 답이 나오나?

The key mechanism: in the real data, $y = P \cdot u$ (plant maps input to output). So:

핵심 메커니즘: 실제 데이터에서 $y = P \cdot u$ (플랜트가 입력을 출력으로 매핑). 따라서:

$$e_v = (M^{-1} - 1) \cdot y = (M^{-1} - 1) \cdot P \cdot u$$

When we solve $u = C \cdot e_v$, substituting gives $u = C \cdot (M^{-1} - 1) \cdot P \cdot u$. Dividing both sides by $u$: $1 = C \cdot (M^{-1} - 1) \cdot P$, which rearranges to $C = M / [P(1-M)]$ — exactly the ideal controller $C^*$ from §4.2. The plant $P$ appears in the equation but is "encoded" in the data, so the regression recovers $C^*$ without ever identifying $P$ explicitly.

$u = C \cdot e_v$에 대입하면 $u = C \cdot (M^{-1} - 1) \cdot P \cdot u$. 양변을 $u$로 나누면: $1 = C \cdot (M^{-1} - 1) \cdot P$, 정리하면 $C = M/[P(1-M)]$ — §4.2의 이상적 제어기 $C^*$와 정확히 동일. $P$는 데이터에 "인코딩"되어 있어 회귀가 $P$를 명시적으로 식별하지 않고 $C^*$를 복원.

Formal proof requires showing that as $N \to \infty$, $\arg\min J(\theta) \to C^*$ under mild conditions. See Campi et al. (2002) Theorem 1.

### 4.6 For PID — It's Just Linear Regression / PID의 경우 — 선형 회귀

Substituting the PID form $C = \rho_1 + \rho_2/s + \rho_3 s$:

$$J = \sum_t \left[ u(t) - \rho_1 \cdot e_v(t) - \rho_2 \cdot \int e_v(t) - \rho_3 \cdot \dot{e}_v(t) \right]^2$$

This is **linear in $\rho$**. Define:

$$\text{target: } b = u, \quad \text{regressors: } \phi_1 = e_v, \quad \phi_2 = \int e_v, \quad \phi_3 = \dot{e}_v$$

Then $J = \| b - \rho_1 \phi_1 - \rho_2 \phi_2 - \rho_3 \phi_3 \|^2$

### 4.7 Solving Linear Regression — Intuition / 선형 회귀 풀기 — 직관

**What is linear regression?** Find numbers $\rho_1, \rho_2, \rho_3$ such that:

$$b \approx \rho_1 \phi_1 + \rho_2 \phi_2 + \rho_3 \phi_3$$

Think of it as: "what mix of $\phi_1, \phi_2, \phi_3$ best approximates $b$?"

"$\phi_1, \phi_2, \phi_3$를 어떤 비율로 섞으면 $b$에 가장 가까운가?"

**Matrix form:** Stack all samples into a matrix:

$$\underbrace{\begin{bmatrix} \phi_1(1) & \phi_2(1) & \phi_3(1) \\ \vdots & \vdots & \vdots \\ \phi_1(N) & \phi_2(N) & \phi_3(N) \end{bmatrix}}_{\Phi} \underbrace{\begin{bmatrix} \rho_1 \\ \rho_2 \\ \rho_3 \end{bmatrix}}_{\rho} \approx \underbrace{\begin{bmatrix} b(1) \\ \vdots \\ b(N) \end{bmatrix}}_{b}$$

**The solution (OLS = Ordinary Least Squares):**

$$\rho = (\Phi^T \Phi)^{-1} \Phi^T b$$

**Intuition:** $\Phi^T b$ measures "how much does each $\phi$ correlate with $b$". $(\Phi^T \Phi)^{-1}$ corrects for correlations between the $\phi$'s themselves.

$\Phi^T b$는 "각 $\phi$가 $b$와 얼마나 상관있는가". $(\Phi^T \Phi)^{-1}$은 $\phi$들 사이의 상관을 보정.

**QR decomposition** is a numerically stable way to compute this. It avoids computing $(\Phi^T \Phi)^{-1}$ directly, which can be inaccurate for ill-conditioned matrices.

**QR 분해**는 이걸 수치적으로 안정적으로 계산하는 방법. $(\Phi^T \Phi)^{-1}$을 직접 계산하면 조건이 나쁜 행렬에서 부정확.

### 4.8 Column Scaling — Why It's Needed / 컬럼 스케일링 — 왜 필요한가

The three regressors have very different magnitudes:
- $\phi_1 = e_v$ : typically $O(1)$
- $\phi_2 = \int e_v$ : accumulates over time → $O(10)$ or more
- $\phi_3 = \dot{e}_v$ : amplifies changes → $O(50)$ (roughly $1/\Delta t$)

세 회귀변수의 크기가 매우 다름:
- $\phi_1 = e_v$ : 보통 $O(1)$
- $\phi_2 = \int e_v$ : 시간에 따라 누적 → $O(10)$ 이상
- $\phi_3 = \dot{e}_v$ : 변화를 증폭 → $O(50)$ (대략 $1/\Delta t$)

**Problem:** If one column is 50× larger, the regression focuses on fitting that column and ignores the others.

문제: 한 열이 50배 크면 회귀가 그 열에 집중하고 나머지를 무시.

**Solution:** Normalize each column by its RMS (root mean square):

$$s_j = \sqrt{\frac{1}{L}\sum_{i=1}^{L} \phi_j[i]^2}, \quad \tilde{\phi}_j = \phi_j / s_j$$

After regression on normalized columns, unscale the result:

$$\rho_1 = \tilde{\rho}_1 \cdot s_b / s_1, \quad \rho_2 = \tilde{\rho}_2 \cdot s_b / s_2, \quad \rho_3 = \tilde{\rho}_3 \cdot s_b / s_3$$

(In the code, $\rho_2$ and $\rho_3$ are called $a_I$ and $a_D$ respectively.)

(코드에서 $\rho_2$, $\rho_3$를 각각 $a_I$, $a_D$로 표기합니다.)

### 4.9 Condition Number — Data Quality Check / 조건수 — 데이터 품질 체크

$$\kappa = \sigma_{max} / \sigma_{min}$$

$\sigma$ values come from **SVD (Singular Value Decomposition)** of $\Phi$, which reveals how "spread out" the data is in each direction.

$\sigma$ 값은 $\Phi$의 **SVD**에서 나오며, 데이터가 각 방향으로 얼마나 "펼쳐져" 있는지를 나타냄.

- $\kappa$ small (< $10^3$): data covers all directions well → good regression
- $\kappa$ large (> $10^6$): data is nearly flat in one direction → can't distinguish P, I, D contributions → unreliable

If $\kappa$ is too large, it means **excitation was insufficient** — the vehicle wasn't wiggled enough at enough different frequencies.

$\kappa$가 크면 **가진이 부족** — 충분히 다양한 주파수로 흔들지 않았다는 뜻.

---

## 5. Prefilter F — Why and How / 프리필터 F — 왜, 어떻게

### 5.1 The Problem Without F / F 없이의 문제

The regression $\min \|u - C(\theta) e_v\|^2$ weights all frequencies equally. But:
- At very low frequencies, the signal is dominated by drift (uninformative)
- At very high frequencies, noise dominates
- PID mostly operates in the **mid-frequency** range

F 없이는 회귀가 모든 주파수에 동일 가중. 하지만 저주파는 드리프트, 고주파는 노이즈가 지배. PID는 **중간 주파수**에서 주로 동작.

### 5.2 Optimal Choice / 최적 선택

Campi et al. (2002) showed the optimal prefilter is:

$$F(s) = M(s)(1 - M(s))W(s)$$

**Why this form? / 왜 이 형태?**

- $M(s)$: low-pass (passes DC, suppresses high freq)
- $(1 - M(s))$: equals 0 at DC, peaks at mid freq → this is the **sensitivity function** (how much error remains). It emphasizes frequencies where the controller has the most effect.
- $W(s)$: additional low-pass filter to suppress measurement noise

- $M(s)$: 저역통과 (DC 통과, 고주파 억제)
- $(1-M(s))$: DC에서 0, 중간 주파수에서 피크 → **감도함수** (오차가 남는 정도). 제어기가 가장 효과적인 주파수를 강조
- $W(s)$: 추가 저역통과 → 측정 노이즈 억제

**Bandpass result / 밴드패스 결과:** $F$ is zero at DC, peaks in the middle, and dies at high frequency. The regression focuses exactly where PID matters most.

$F$는 DC에서 0, 중간에서 피크, 고주파에서 감소. 회귀가 PID가 가장 중요한 곳에 집중.

### 5.3 The Weighting Filter W / 가중 필터 W

$$W(s) = \frac{\omega_W}{s + \omega_W}, \quad \omega_W = 2\pi f_W, \quad f_W = f_s / 8$$

This is a 1st-order low-pass filter. At the cutoff frequency $f_W$, the gain is $-3$ dB (about 70%). Above $f_W$, signals are attenuated at $-20$ dB per decade.

1차 저역통과 필터. 컷오프 주파수 $f_W$에서 게인 -3dB (약 70%). $f_W$ 위에서 10배 주파수당 -20dB 감쇠.

---

## 6. Reference Model M / 참조 모델 M

### 6.1 What M Represents / M이 뜻하는 것

$M(s)$ describes **how we want the closed loop to respond**. If we apply a step input:
- How fast should $y$ reach the target? → $T_s$ (settling time)
- Should it overshoot? → controlled by order $n_M$
- Is there a delay before anything happens? → $\tau$ (dead time)

$M(s)$은 **폐루프가 어떻게 응답하길 원하는지** 기술. 스텝 입력 시:
- 얼마나 빨리 목표에 도달? → $T_s$ (정착시간)
- 오버슈트? → 차수 $n_M$으로 조절
- 반응 전 지연? → $\tau$ (순수 지연)

### 6.2 The Formula / 수식

$$M(s) = \frac{e^{-\tau s}}{(1 + 0.2 T_s \cdot s)^2}$$

**Why include $\tau$ in $M$? / 왜 $M$에 $\tau$를 포함?** If $M$ has no delay but the real plant does, VRFT demands "respond instantly" — which is physically impossible. The controller would try infinite gain to compensate, giving unrealistic results. Including $\tau$ tells VRFT "it's OK to wait $\tau$ seconds before responding."

$M$에 지연이 없는데 실제 플랜트에 있으면, VRFT가 "즉시 반응하라"를 요구 → 물리적으로 불가능 → 비현실적 게인. $\tau$를 넣으면 "$\tau$초 대기 후 반응해도 됨"이라고 알려주는 것.

### 6.3 Where Does 0.2 Come From? / 0.2는 어디서?

For a 2nd-order system $G(s) = 1/(1 + \tau_m s)^2$, the step response reaches 95% of its final value at time:

2차 시스템 $G(s) = 1/(1 + \tau_m s)^2$의 스텝 응답이 최종값의 95%에 도달하는 시간:

$$t_{95\%} \approx 5 \tau_m$$

This is the **settling time** $T_s$. So:

$$T_s = 5\tau_m \implies \tau_m = T_s / 5 = 0.2 \cdot T_s$$

**Derivation:** The step response of $(1/(1+\tau_m s))^2$ is $1 - (1 + t/\tau_m)e^{-t/\tau_m}$. Setting this to 0.95 and solving gives $t \approx 4.74\tau_m \approx 5\tau_m$.

$(1/(1+\tau_m s))^2$의 스텝 응답은 $1 - (1 + t/\tau_m)e^{-t/\tau_m}$. 0.95로 놓고 풀면 $t \approx 5\tau_m$.

### 6.4 Why $n_M = 2$? / 왜 $n_M = 2$?

FTD vehicles are physical objects with **inertia** (resists rotation) and **damping** (air resistance slows rotation). This is naturally a 2nd-order system:

FTD 기체는 **관성** (회전에 저항)과 **감쇠** (공기저항이 회전을 늦춤)를 가진 물리 객체 → 2차 시스템:

$$P(s) \approx \frac{K}{s(\tau_p s + 1)} \quad \text{(inertia + damping / 관성 + 감쇠)}$$

where $\tau_p$ is the plant's time constant (NOT the pure delay $\tau$ from §6.2).

$\tau_p$는 플랜트의 시정수 (§6.2의 순수 지연 $\tau$와 다름).

### 6.5 What Is τ (Pure Delay)? / τ (순수 지연)이란?

$\tau$ is the time between giving a command and **anything happening at all**. Not "slow response" — literally zero response for $\tau$ seconds.

명령을 주고 **아무 반응도 없는** 시간. "느린 응답"이 아니라 $\tau$초 동안 문자 그대로 무반응.

Example: A thruster that takes 0.1s to start producing force after commanded. During that 0.1s, the output is exactly zero.

예: 명령 후 추력 생산까지 0.1초 걸리는 추력기. 그 0.1초 동안 출력이 정확히 0.

---

## 7. Computation Pipeline / 계산 파이프라인

### Step 1: Detrend / 디트렌드

Remove the average value (DC) and any linear drift from $u$ and $y$.

$u$, $y$에서 평균값(DC)과 선형 드리프트를 제거.

**Why:** FFT assumes the signal repeats forever. If the signal starts at 0 and ends at 5 (a drift), the FFT sees a huge "jump" from 5 back to 0, creating fake high-frequency content (**spectral leakage**).

FFT는 신호가 무한 반복한다고 가정. 0에서 시작해서 5로 끝나면 5→0 "점프"를 보고 가짜 고주파를 만듦 (**스펙트럴 리키지**).

### Step 2: Zero-Padded FFT / 제로패딩 FFT

Append zeros to make length $N_{FFT} = 2^{\lceil \log_2(2N) \rceil}$.

**Why zero-pad? / 왜 제로패딩?** FFT-based filtering produces **circular convolution** (signal wraps around). Adding zeros converts this to **linear convolution** (no wrap-around artifacts).

FFT 필터링은 **순환 합성곱**을 만듦 (신호가 감김). 0을 추가하면 **선형 합성곱**으로 변환 (감김 없음).

**Why power of 2? / 왜 2의 거듭제곱?** FFT is fastest when the length is a power of 2.

### Step 3: Apply M, W, F in Frequency Domain / 주파수 영역에서 M, W, F 적용

*Notation: uppercase ($U, Y, R_v, E_v$) = frequency domain; lowercase ($u, y, r_v, e_v$) = time domain.*

*표기: 대문자 ($U, Y, R_v, E_v$) = 주파수 영역; 소문자 ($u, y, r_v, e_v$) = 시간 영역.*

For each frequency bin $k$ (each sine wave component):

1. Compute $M(j\omega_k)$, $W(j\omega_k)$, $F(j\omega_k) = M(1-M)W$
2. Virtual reference: $R_v = Y / M$
3. Virtual error: $E_v = R_v - Y = (1/M - 1) \cdot Y$
4. Filter both sides: $U_F = F \cdot U$, $E_F = F \cdot E_v$

**Why frequency domain?** Multiplying spectra = filtering in time domain, but much faster. Also, $M^{-1}$ involves **looking into the future** (non-causal due to $e^{\tau s}$), which is impossible in time domain but trivial in frequency domain (just multiply by a complex number).

주파수 영역에서 곱셈 = 시간 영역 필터링이지만 훨씬 빠름. 또한 $M^{-1}$은 **미래를 봐야 함** (비인과적), 시간 영역에서 불가능하지만 주파수 영역에서는 복소수 곱셈 하나.

### Step 4: IFFT Back to Time / 역FFT → 시간 영역

Convert $U_F$ and $E_F$ back to time signals. Use only the first $N$ samples (the rest are zero-padding artifacts).

### Step 5: Build Regression Vectors / 회귀 벡터 구성

From the filtered virtual error $e_F$:

**P term (proportional):**
$$\phi_1[i] = e_F[i]$$

**I term (integral) — trapezoidal rule:**
$$\phi_2[i] = \phi_2[i-1] + \frac{e_F[i-1] + e_F[i]}{2} \Delta t$$

*Trapezoidal rule: approximate the area under the curve as a series of trapezoids. More accurate than just rectangles ($O(\Delta t^2)$ vs $O(\Delta t)$).*

*사다리꼴 규칙: 곡선 아래 면적을 사다리꼴로 근사. 직사각형보다 정확 ($O(\Delta t^2)$ vs $O(\Delta t)$).*

**D term (derivative) — central difference:**
$$\phi_3[i] = \frac{e_F[i+1] - e_F[i-1]}{2\Delta t}$$

*Central difference: estimate slope using points on both sides. More accurate than one-sided difference ($O(\Delta t^2)$ vs $O(\Delta t)$).*

*중심 차분: 양쪽 점으로 기울기 추정. 한쪽 차분보다 정확.*

**Target:**
$$b[i] = u_F[i]$$

### Step 6–9: Scale, Check, Solve, Extract / 스케일, 검사, 풀기, 추출

6. **Scale** columns by RMS (see §4.8)
7. **Check** condition number (see §4.9)
8. **Solve** $\tilde{b} \approx \tilde{\rho}_1 \tilde{\phi}_1 + \tilde{\rho}_2 \tilde{\phi}_2 + \tilde{\rho}_3 \tilde{\phi}_3$ via QR
9. **Extract:** $K_p = \rho_1$, $T_i = K_p / a_I$, $T_d = a_D / K_p$

---

## 8. Ti Limitation / Ti 한계

### Why VRFT Can't Estimate Ti Well / VRFT가 Ti를 잘 못 구하는 이유

$$F(0) = M(0) \cdot (1 - M(0)) \cdot W(0) = 1 \times 0 \times W(0) = 0$$

The prefilter $F$ is **exactly zero at DC**. This means all steady-state (constant offset) information is removed before the regression even starts.

프리필터 $F$가 **DC에서 정확히 0**. 정상상태(일정 오프셋) 정보가 회귀 시작 전에 완전히 제거.

$T_i$ is the parameter that controls steady-state error correction. Without DC information, the regression coefficient $a_I$ (= $K_p / T_i$) is essentially fitting noise.

$T_i$는 정상상태 오차 보정을 제어하는 파라미터. DC 정보 없이 $a_I$는 사실상 노이즈를 피팅.

**This is not a bug — it's inherent to VRFT's design.** $M(0) = 1$ means "zero steady-state error", which is a good thing. But $1 - M(0) = 0$ kills DC in the prefilter. You can't have both.

### Fallback / 대체

When $T_i$ from VRFT is unreliable ($< 0.1$ or $> 100$):

$$T_i = 4\tau + T_s$$

This is an empirical heuristic. Relay feedback's $T_i = T_u/2$ is theoretically better because it measures $T_i$ directly from oscillation data without the DC=0 problem.

경험적 규칙. 릴레이 피드백의 $T_i = T_u/2$가 이론적으로 더 나음 — DC=0 문제 없이 진동 데이터에서 직접 측정.

---

## 9. Relay Feedback — Initial PID / 릴레이 피드백 — 초기 PID

### 9.1 When to Use / 언제 사용

When PID is unknown or so bad that the vehicle oscillates wildly or saturates constantly. VRFT needs reasonable data, which requires a somewhat stable system.

### 9.2 Setup / 설정

During relay test:
- **I is disabled** ($T_i = 250$ = off)
- **D is disabled** ($T_d = 0$)
- **Only P-gain $K_p$ remains** (for basic stability)
- **Integral state is reset** (to prevent windup)

The loop becomes: $(SP \pm h) \to K_p \to Plant \to y$

### 9.3 Relay Operation / 릴레이 작동

A square wave $\pm h$ is added to the setpoint, switching whenever $y$ crosses the center:

$$SP = SP_{base} + \begin{cases} +h & \text{if } y > y_{center} \\ -h & \text{if } y < y_{center} \end{cases}$$

This is intentionally **positive feedback on SP** — when $y$ is above center, SP is pushed further up, which forces sustained oscillation. (The P-controller provides negative feedback to keep it bounded.)

이것은 의도적으로 **SP에 양의 피드백** — $y$가 중심 위에 있으면 SP를 더 올려서 지속적 진동을 유도. (P 제어기가 음의 피드백으로 발산을 방지.)

From the oscillation we measure:
- **Period $T_u$** from zero-crossing times
- **Amplitude $a$** of $y$ oscillation

### 9.4 Computing $K_u$ / $K_u$ 산출

The relay output is a square wave. A square wave's **Fourier series** is:

$$\text{square}(t) = \frac{4h}{\pi}\sin(\omega t) + \frac{4h}{3\pi}\sin(3\omega t) + \cdots$$

The fundamental (first) component has amplitude $\frac{4h}{\pi}$.

구형파의 **푸리에 급수**: 기본 성분 진폭이 $\frac{4h}{\pi}$.

At the oscillation frequency, the plant's gain exactly balances this. Let $K_{p,old}$ denote the **existing P-gain kept during relay test** (from §9.2):

진동 주파수에서 플랜트 게인이 정확히 균형. $K_{p,old}$는 **릴레이 테스트 중 유지된 기존 P 게인** (§9.2):

$$|K_{p,old} \cdot P(j\omega_u)| \cdot \frac{4h}{\pi} = a$$

Since the relay sees $K_{p,old} \cdot P$ (not just $P$), the measured ultimate gain is for the combined system:

$$K_u^{meas} = \frac{4h}{\pi a}$$

The plant's true ultimate gain (correcting for $K_{p,old}$):

$$K_u^{plant} = K_u^{meas} \times K_{p,old}$$

### 9.5 Ziegler-Nichols Rules / Ziegler-Nichols 규칙

New PID parameters (**$K_{p,new}$**, not to be confused with $K_{p,old}$):

새 PID 파라미터 (**$K_{p,new}$**, $K_{p,old}$와 혼동 주의):

$$K_{p,new} = 0.6 \, K_u^{plant}, \quad T_i = T_u / 2, \quad T_d = T_u / 8$$

Note: **$T_i$ is computed directly** from $T_u$ — no DC limitation like VRFT.

$T_i$가 $T_u$에서 **직접 산출** — VRFT 같은 DC 한계 없음.

### 9.6 Adaptive Amplitude / 적응형 진폭

Start: $h = \max(2.0, 3 \times \text{std}_{natural})$. If < 2 crossings after 5s, double $h$ (max 10.0).

### 9.7 Safety / 안전

- P-gain maintained → basic stability / P 게인 유지 → 기본 안정성
- Timeout 30s → original PID restored / 30초 타임아웃 → 원래 PID 복원

---

## 10. Automatic Parameter Estimation / 자동 파라미터 추정

### 10.1 Delay τ — Phase Slope / 지연 τ — 위상 기울기

A pure delay shifts the phase linearly: $\angle H(j\omega) = -\omega\tau$

순수 지연은 위상을 선형으로 이동: $\angle H(j\omega) = -\omega\tau$

Estimate $H(j\omega) = Y/U$ with Wiener regularization, then fit a line through the low-frequency phase:

$$\tau = -\frac{\sum w_k \omega_k \phi_k}{\sum w_k \omega_k^2}$$

**Phase unwrapping / 위상 언래핑:** `atan2` returns $(-\pi, \pi]$. For large delays, the phase wraps around (jumps from $-\pi$ to $+\pi$). We detect jumps > $\pi$ between adjacent bins and apply $\pm 2\pi$ corrections.

`atan2`는 $(-\pi, \pi]$ 반환. 큰 지연에서 위상이 감김. 인접 빈 간 $\pi$ 넘는 점프 감지 시 $\pm 2\pi$ 보정.

### 10.2 Settling Time $T_s$ — Parameter Stability / 정착 시간 — 파라미터 안정성

Scan $T_s$ from 0.1 to 1.0 (log scale, 40 steps). At each point, compute PID. Find the smallest $T_s$ where adjacent results don't change more than 30%:

$$\max\left(\frac{|\Delta K_p|}{|K_p|}, \frac{|\Delta T_i|}{|T_i|}, \frac{|\Delta T_d|}{|T_d|}\right) < 0.3$$

**Intuition:** Near the boundary where $K_p$ flips to negative, tiny changes in $T_s$ cause huge parameter swings → numerically unreliable. The first stable point is the fastest response the data can support.

경계 근처에서 $T_s$를 살짝 바꿔도 파라미터가 크게 변동 → 수치적으로 신뢰 불가. 첫 안정점이 데이터가 지원하는 가장 빠른 응답.

**Why 30%? / 왜 30%?** A 30% change in PID parameters produces a barely noticeable difference in actual control performance for most systems. Below this threshold, the solution is "practically the same" regardless of small $T_s$ variations. The value is empirical but conservative — tighter thresholds (e.g., 10%) would select larger $T_s$ (slower response).

30% PID 파라미터 변화는 대부분의 시스템에서 실제 제어 성능 차이가 거의 체감 불가. 이 이하면 $T_s$가 약간 변해도 "실질적으로 같은" 해. 경험적이지만 보수적 — 더 엄격한 임계값(예: 10%)은 더 큰 $T_s$(느린 응답)를 선택.

---

## 11. Excitation — Multi-Sine / 가진 — 멀티사인

### 11.1 Why Excitation Is Needed / 왜 가진이 필요한가

The regression matrix $\Phi$ has three columns ($e_v$, $\int e_v$, $\dot{e}_v$). For reliable regression, these columns must be **linearly independent** — they must contain different information.

회귀 행렬 $\Phi$의 세 열이 **선형 독립**이어야 함 — 서로 다른 정보를 담고 있어야.

With a single frequency $\omega_0$: $\int e_v \sim \cos(\omega_0 t)/\omega_0$ and $\dot{e}_v \sim \cos(\omega_0 t) \cdot \omega_0$ — they're the **same shape** just scaled differently. The matrix is nearly singular → can't separate P, I, D.

단일 주파수에서는 $\int e_v$와 $\dot{e}_v$가 **같은 형태** (스케일만 다름) → 행렬이 거의 특이(singular) → P, I, D 분리 불가.

Multiple frequencies make the integral and derivative responses diverge → linearly independent → reliable regression.

다양한 주파수는 적분·미분 응답을 다르게 만듦 → 선형 독립 → 신뢰할 수 있는 회귀.

### 11.2 Multi-Sine Formula / 멀티사인 수식

$$x(t) = \sum_{k=0}^{11} \frac{A}{\sqrt{12}} \sin(2\pi f_k t + \phi_k)$$

**12 frequencies, logarithmically spaced:**

$$f_k = 0.05 \times (2.0/0.05)^{k/11} \quad \text{Hz} \quad (0.05 \text{ to } 2 \text{ Hz})$$

**Why log spacing? / 왜 로그 간격?** PID operates mostly at low frequencies. Log spacing puts more components there (0.05, 0.07, 0.1, 0.14, ...) and fewer at high frequencies where D-term causes saturation.

PID는 주로 저주파에서 동작. 로그 간격이 저주파에 더 많은 성분을 배치.

### 11.3 Schroeder Phase — Reducing Peak Amplitude / 슈뢰더 위상 — 피크 진폭 감소

$$\phi_k = -\frac{\pi k(k+1)}{12}$$

**Why?** When you add 12 sine waves, they can occasionally all peak at the same time, creating a huge spike. This spike can cause saturation.

12개 사인파를 더하면 가끔 동시에 피크가 올 수 있음 → 큰 스파이크 → 포화.

**Schroeder (1970) proved** that this specific phase assignment minimizes the peak-to-RMS ratio (**crest factor**). Random phases give crest factor ~$\sqrt{2N} \approx 4.9$, Schroeder gives ~$\sqrt{2} \approx 1.4$. That's **~40% lower peak** for the same RMS energy.

이 특정 위상 배치가 피크/RMS 비(**크레스트 팩터**)를 최소화함을 증명. 랜덤 위상 대비 **피크 ~40% 감소**.

**Intuition:** The phases are chosen so that different components peak at different times, spreading the energy evenly over time instead of concentrating it.

직관: 다른 성분들이 다른 시간에 피크가 오도록 위상을 배치 → 에너지가 시간에 걸쳐 균등 분산.

### 11.4 Why Not Chirp? / 왜 Chirp이 아닌가?

Chirp sweeps frequencies one at a time. When it reaches high frequencies, the D-term amplifies the rapid changes → saturation. Multi-sine excites all frequencies simultaneously → no buildup at any frequency.

Chirp는 주파수를 순차적으로 스윕. 고주파에서 D항이 급변을 증폭 → 포화. 멀티사인은 모든 주파수를 동시에 → 특정 주파수에서 축적 없음.

---

## 12. Saturation Handling / 포화 처리

### 12.1 Why Saturation Breaks Everything / 포화가 모든 걸 망치는 이유

When $|u| = 1$, the PID wanted to output more (maybe $u = 3$) but the actuator is capped. The recorded $u = 1$ doesn't reflect the PID's intent. The regression sees "small input, big response" → thinks the plant has high gain → underestimates $K_p$.

$|u| = 1$일 때 PID는 더 내보내고 싶었지만 (예: $u = 3$) 구동기가 한계. 기록된 $u = 1$이 의도를 반영 못 함 → "작은 입력, 큰 응답" → 플랜트 게인 과대추정 → $K_p$ 과소추정.

### 12.2 Desaturation (Pre-Recording) / 포화 해소 (수집 전)

1. Measure saturation rate for 1 second
2. If ≥ 10%: halve $K_p$, reset integral state, re-measure
3. Repeat until < 10% or $K_p$ < 0.001 (at minimum $K_p$, recording proceeds anyway with best-effort data / 최소 $K_p$에서는 최선의 데이터로 녹화 진행)

**Why reset integral?** Integral accumulates error over time (windup). Even after reducing $K_p$, the accumulated integral can keep pushing $u$ to saturation. Resetting clears this.

적분은 시간에 따라 오차를 누적 (와인드업). $K_p$를 줄여도 누적된 적분이 $u$를 포화로 계속 밀 수 있음. 리셋으로 해소.

### 12.3 During Recording / 수집 중

- Consecutive saturation ≥ 10 ticks → block split
- ≥ 3 splits → stop, halve $K_p$ + reset integral, restart

### 12.4 Real-Time Avoidance / 실시간 회피

$$\text{amp\_scale} = \text{clamp}\left(\frac{0.98 - |u|}{0.3},\ 0.1,\ 1.0\right)$$

Excitation amplitude scaled down as $|u|$ approaches 0.98. Smooth reduction, not sudden cutoff.

---

## 13. FTD-Specific / FTD 특이사항

### Axis SP/PV Structure / 축별 SP/PV 구조

| Axis | SP | PV | VRFT OK? |
|------|----|----|----------|
| Pitch | Target angle | Current angle | ✅ |
| Roll | Target angle | -Current angle | ✅ (sign flip) |
| Yaw | Usually 0 | Angle error | ✅ (error = -actual) |
| Hover | Target altitude | Current altitude | ✅ |
| Forward | 0 | Distance/speed | ✅ |
| Strafe | 0 | Lateral offset | ✅ |

VRFT only uses $u$ and $y$ — SP structure doesn't matter.

### Tuning Order / 튜닝 순서

Roll → Pitch → Yaw → Hover/Forward/Strafe. Fast axes first, coupling reduces as inner axes stabilize.

### Environment / 환경

Tune at low altitude (thick atmosphere). More air resistance = more damping = more stability margin. PID tuned in thick air works in thin air, but not vice versa.

---

## 14. Known Limitations / 알려진 한계

| Limitation | Cause | Mitigation |
|-----------|-------|------------|
| $T_i$ inaccurate | $F(0) = 0$ (VRFT structural) | Relay feedback or manual adjustment |
| Conservative $K_p$ in maneuvers | Linear assumption violated | Tune in stable flight |
| Cross-axis coupling | SISO method | Sequential tuning |
| Requires stable initial PID | Data quality dependency | Use relay feedback first |
| Results vary between runs | Data-dependent | Repeat in stable conditions |

---

## 15. Summary Table / 정리표

| Component | From paper? | Basis |
|-----------|-------------|-------|
| $M, W, F$ filters | ✅ | Campi et al. 2002 |
| Virtual reference $r_v = M^{-1}y$ | ✅ | Core VRFT |
| OLS regression | ✅ | Core VRFT |
| $T_i$ fallback $4\tau + T_s$ | ⚠️ | Empirical heuristic |
| Relay feedback (P-only + $K_u$ correction) | ❌ | Åström-Hägglund 1984, modified |
| $\tau$ phase slope + unwrapping | ❌ | Standard sysid + signal processing |
| $T_s$ parameter stability search | ❌ | Robust optimization concept |
| Multi-sine (Schroeder phase) | ❌ | Schroeder 1970, industrial standard |
| Adaptive amplitude | ❌ | SNR-based engineering |
| Desaturation + integral reset | ❌ | ISA PID property |
| Saturation avoidance scaling | ❌ | Constraint-aware excitation |
