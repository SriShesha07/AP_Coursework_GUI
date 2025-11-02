### How to test the plotting feature (GUI2)

This guide explains how to manually verify the plotting functionality added to the application and, optionally, how to validate the backend logic without UI automation.

---

#### What are Xmin, Xmax, and Step?
- Xmin: The left boundary of the x-range you want to sample for plotting.
- Xmax: The right boundary of the x-range (must be strictly greater than Xmin).
- Step: The distance between successive sample points along the x-axis (must be > 0). Smaller Step = more points = smoother curve, but slower.

Why needed? Plotting works by evaluating your last expression (which should depend on x) at many x-values between Xmin and Xmax. These three inputs tell the GUI which x-values to use. They are part of Task GUI2 (plotting UI) and are not mentioned in Task INT2, which focused on variables and functions.

---

#### 1) Build and run
- Open the AP_Coursework_GUI.sln in Rider/Visual Studio.
- Select AP_Coursework_GUI as startup project (WPF).
- Build and Run. The window should show:
  - Multi-line text input
  - Evaluate button
  - Xmin, Xmax, Step inputs
  - Plot button
  - Result and Errors text boxes
  - A white canvas area bordered for plotting

---

#### 2) Basic manual plotting tests
Use invariant formatting for ranges (e.g., -10, 10, 0.5). Input can be separated by newlines or semicolons.

A. Straight line
- Input: `a=2; b=1; a*x + b`
- Range: Xmin=-10, Xmax=10, Step=0.5
- Expect a straight line with slope ~2, y-intercept ~1. Axes should appear because 0 is within the range.

B. Parabola (polynomial)
- Input: `f(x) = x^2 + 2*x + 1; f(x)`
- Range: Xmin=-5, Xmax=5, Step=0.25
- Expect a U-shaped curve (vertex at x=-1, y≈0). Symmetric around x=-1.

C. Function call with variable shift
- Input: `h(z)=z^2; c=3; h(x+c)`
- Range: Xmin=-5, Xmax=5, Step=0.5
- Expect a parabola shifted left by 3 (minimum near x=-3).

D. Visibility of axes
- Enter any expression and try:
  - Range including 0 (e.g., [-5,5]) → both axes visible when their 0 lies in range.
  - Range excluding 0 (e.g., [1,5]) → y-axis not drawn; [ -5,-1 ] → x-axis may be outside view.

E. Step density
- Plot the same expression with Step=1 vs Step=0.1 and observe line smoothness. Smaller step → smoother (more points).

---

#### 3) Error handling and edge cases

A. Input validation
- Xmin="a" or Xmax="b" or Step="zero" → ErrorBox shows: "Invalid range values..."
- Step ≤ 0 or Xmax ≤ Xmin → ErrorBox shows: "Ensure: Step > 0 and Xmax > Xmin."

B. Undefined variable in setup/expr
- Input: `a=2; a*x + b` (missing b)
- Expect: "Setup error: Runtime error: Undefined variable: b" (if in setup) or an error during sampling mentioning the x where it failed.

C. Division by zero inside range
- Input: `1/(x-1)` with Xmin=0, Xmax=2, Step=0.1
- Expect: Error shown like: `At x=1: Runtime error: Division by zero`. Plot stops without drawing.

D. Flat function (y-range auto expansion)
- Input: `0*x`
- Expect: Draws a horizontal line; if y-range is constant, the app expands it slightly to render.

E. Large ranges
- Input: `x`
- Range: Xmin=-1000, Xmax=1000
- Expect: Line spanning the canvas; axes centered; performance remains acceptable.

---

#### 4) Cross-checking correctness
- Use Evaluate for spot checks:
  1) Set `a=2; b=1` on one line and click Evaluate to confirm a and b are stored.
  2) Enter a test x (temporarily): you can simulate by evaluating an expression like `(a*3 + b)` to expect `7`.
  3) Compare intercepts on the plot: for `a*x+b`, y-intercept is b (where x=0). Check the plotted line crosses the x-axis where `a*x+b=0`.

---

#### 5) Backend testing without UI (optional)
You can sanity-check the evaluator used by plotting using the following patterns in F# Interactive or unit tests:

- Evaluate a function body at a given x:
  - `ExprEvaluator.ResetState()`
  - Define setup via `ExprEvaluator.EvaluateExpression("a=2; b=1")`
  - Sample y: `ExprEvaluator.EvaluateExprForX("a*x+b", 3.0)` → "7"

- Function definitions:
  - `ExprEvaluator.ResetState()`
  - `ExprEvaluator.EvaluateExpression("f(x)=x^2+2*x+1")`
  - `ExprEvaluator.EvaluateExprForX("f(x)", -1.0)` → "0"

- Error strings (should start with "Lexer"/"Parser"/"Runtime"):
  - `ExprEvaluator.EvaluateExprForX("1/(x-1)", 1.0)` → "Runtime error: Division by zero"

If you prefer, add a test project (xUnit/NUnit) targeting net8.0 and assert on these API results.

---

#### 6) Troubleshooting tips
- Nothing drawn: ensure at least two valid points were sampled (Step not too large; expression valid for most x).
- Canvas size: if the canvas seems tiny, resize the window; the code defaults to 800x400 for very small sizes.
- Locale: numbers use invariant culture; enter decimal points using dot (e.g., 0.5), not comma.

---

#### 7) Example cheat sheet
- `a=2; b=1; a*x + b` → line
- `f(x)=x^2+2*x+1; f(x)` → parabola
- `g(t)=t^3-4*t; g(x)` → cubic
- `h(z)=z^2; c=3; h(x+c)` → shifted parabola
