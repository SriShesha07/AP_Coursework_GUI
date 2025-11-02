### Expression Interpreter and Plotting GUI — Full Technical Guide

This document explains, end‑to‑end, how the project works: the grammar (BNF), lexing, parsing, evaluation, state management (variables and functions), and the GUI plotting pipeline. Reading this should enable a new contributor to understand and extend the system.

---

### Project overview
- Purpose: Provide a small expression language that supports variables, single‑argument functions, arithmetic, and plotting expressions in `x`.
- Technologies:
  - Backend: F# (pure functions + minimal mutable maps for state)
  - GUI: WPF (C#, .NET 8, `net8.0-windows`)
- Key features:
  - Variable assignment and usage: `x = 10` then `x + 5`
  - Single‑parameter function definitions and calls: `f(x) = x^2 + 2*x + 1`, `f(3)`
  - Arithmetic operators: `+ - * / % ^` with precedence and parentheses
  - Plotting an expression of `x` over a range `[Xmin, Xmax]` with step size

---

### Repository layout
- `AP_Coursework/AP_Coursework/Program.fs`
  - F# module `ExprEvaluator` implementing the language: tokens, lexer, parser (recursive descent), evaluator, state, and helper API for the GUI.
- `AP_Coursework_GUI/AP_Coursework_GUI/MainWindow.xaml`
  - WPF window layout: inputs, buttons, canvas.
- `AP_Coursework_GUI/AP_Coursework_GUI/MainWindow.xaml.cs`
  - Code‑behind: integrates UI with `ExprEvaluator`, evaluation and plotting logic, rendering to canvas.
- `AP_Coursework_GUI/AP_Coursework_GUI/TESTING_PLOTTING.md`
  - Manual testing guide for plotting.

---

### Grammar (BNF)
The language is defined with this BNF (also embedded in `Program.fs`):

```
<program>      ::= <statement>
<statement>    ::= <functiondef> | <assignment> | <expression>
<functiondef>  ::= <identifier> "(" <identifier> ")" "=" <expression>
<assignment>   ::= <identifier> "=" <expression>
<expression>   ::= <term> { ("+" | "-") <term> }
<term>         ::= <factor> { ("*" | "/" | "%") <factor> }
<factor>       ::= <primary> [ "^" <factor> ]        ; right‑associative
<primary>      ::= <number>
                 | <identifier>
                 | <identifier> "(" <expression> ")"   ; function call
                 | "(" <expression> ")"
<number>       ::= <digit> { <digit> } [ "." <digit> { <digit> } ]
<identifier>   ::= <letter> { <letter> | <digit> | "_" }
```

Operator precedence (high → low):
- Parentheses/grouping, function calls
- Exponent `^` (right‑associative)
- Unary `+`/`-`
- Multiplicative `* / %`
- Additive `+ -`

---

### Tokens and Lexer
File: `Program.fs`

Token type:
```
type Token =
  | Plus | Minus | Mul | Div | Mod | Pow
  | Lpar | Rpar | Assign
  | Number of float
  | Ident of string
```

Key helpers:
- `isDigit`, `isLetter`, `isWhite`, `isIdentChar`
- `scanNumber`: consumes digits and an optional single `.` to form a numeric literal string
- `scanIdent`: consumes letters/digits/`_` starting with a letter

`lexer (input: string) : Token list` walks the input and emits tokens:
- Whitespace is skipped
- Single‑char operators and punctuation are recognized (`+ - * / % ^ ( ) =`)
- Identifiers: start with letter, continue with letters/digits/underscore → `Ident name`
- Numbers: digits with optional `.` → parsed with `Double.TryParse` → `Number value`
- Errors throw `LexError`: unknown character or invalid number

Why numbers are stored as `float`:
- Simpler numeric pipeline; integer‑like detection is done later for formatting and certain integer operations.

---

### Parser (recursive descent) and where it’s defined
File: `Program.fs`

Entry points:
- Expressions: `parseE` (and its siblings `parseT`, `parseF`, `parseU`, `parseP`)
- Statements: `parseAndEval` contains `parseStatement` to handle assignment and function definition before falling back to expression parsing.

Structure:
- `parseE` (expression): handles `+`/`-` between terms.
- `parseT` (term): handles `*` `/` `%` between factors.
- `parseF` (factor): handles exponentiation `^` with right‑associativity by recursing on the right.
- `parseU` (unary): handles unary `+` and `-`.
- `parseP` (primary): numbers, identifiers, function calls, and parenthesized expressions.

Function definitions and calls:
- Definition is caught in `parseStatement`:
  - Pattern: `Ident fname :: Lpar :: Ident param :: Rpar :: Assign :: rest`
  - Stores `(param, rest)` into `functionTable` under `fname`.
- Call is parsed in `parseP` when seeing `Ident name :: Lpar`:
  - Parses argument expression → finds function by `name` → temporarily binds parameter in `symbolTable` → evaluates function body tokens as a full expression → restores previous binding.

Error handling during parsing:
- Unbalanced parentheses → `ParseError "Missing closing parenthesis"`
- Extra tokens after a complete parse → `ParseError "Extra tokens after expression"`
- Undefined variable or function → raised as `EvalError` during primary resolution.

---

### Evaluation model and where it’s defined
File: `Program.fs`

State (symbol/function tables):
```
let mutable symbolTable = Map.empty<string, float>
let mutable functionTable = Map.empty<string, string * Token list>
```
- `symbolTable` holds variables with their last assigned numeric value
- `functionTable` maps function name → `(parameterName, bodyTokens)`

Evaluation occurs during parsing:
- The parser computes numeric results while constructing them (i.e., an evaluate‑while‑parse style).
- Each parse function returns `(remainingTokens, value: float, isFloat: bool)` where `isFloat` indicates whether the result should be printed with decimal places.

Arithmetic rules and helpers:
- `pow base exp` (integer exponents; negative exponents allowed via reciprocal). Fractional exponents throw `EvalError`.
- `intDiv` and `intMod` implement integer‑like division/modulo semantics with runtime checks for division/modulo by zero.
- Division `/`: if both operands are integer‑like and no float context forces float, division uses `intDiv` (truncate toward zero), else standard float division.
- `%` uses `intMod`.

Float vs integer‑like formatting:
- Helper `isIntLike` checks closeness to an integer.
- The last expression’s display string uses:
  - Integer‑like → `ToString("0", InvariantCulture)`
  - Otherwise → `ToString("0.0###", InvariantCulture)` (at least one decimal, up to 4 additional as needed)

APIs exposed to GUI:
- `EvaluateExpression (input: string) : string`
  - Splits input by newline/`;` and executes sequentially.
  - Returns the last statement’s result as string or an error string starting with `Lexer/Parser/Runtime`.
- `ResetState ()` resets both tables to empty for an isolated plotting session.
- `EvaluateExprForX (expr: string) (x: float) : string`
  - Temporarily binds `x` in `symbolTable`, evaluates `expr`, restores previous `x` binding, and returns a numeric string or error string.

Error classes:
- `LexError`, `ParseError`, `EvalError` are caught at API boundaries and turned into prefixed error messages.

---

### Plotting pipeline and where it’s defined
Files: `MainWindow.xaml` and `MainWindow.xaml.cs`

UI elements (XAML):
- Multiline `TextBox` for input statements
- Buttons: `Evaluate`, `Plot`
- Text boxes for `Xmin`, `Xmax`, `Step`
- Read‑only `Result` and `Errors` displays
- `Canvas` inside a `Border` used as a plotting surface

Event handlers (code‑behind):
- `Evaluate_Click`:
  - Calls `ExprEvaluator.EvaluateExpression` with the entire input; shows result or error in the text boxes.
- `Plot_Click`:
  1) Clears previous output; validates `Xmin`, `Xmax`, `Step` using `double.TryParse` with InvariantCulture.
  2) Splits input by newline/`;` → setup lines (all but last) and a final expression (last line).
  3) Calls `ExprEvaluator.ResetState()` to isolate the plotting session from previous Evaluate runs.
  4) Executes each setup line with `EvaluateExpression`. On any error, shows `Setup error: …` and aborts.
  5) Samples the final expression across `[Xmin, Xmax]` stepping by `Step` using `EvaluateExprForX`.
     - On error at any sample, shows `At x=<value>: <error>` and aborts.
     - Otherwise collects a `List<Point>` with real coordinates.
  6) Calls `DrawPlot(points, xmin, xmax)` to render.

Rendering (`DrawPlot`):
- Determines `width/height` of the canvas (with fallback default sizes if too small).
- Computes `ymin` and `ymax` from sampled points. If the function is flat (`ymax ≈ ymin`), expands slightly to make it visible.
- Defines mapping functions:
  - `xToPx(x) = pad + (x − xmin)/(xmax − xmin) * (width − 2*pad)`
  - `yToPy(y) = height − pad − (y − ymin)/(ymax − ymin) * (height − 2*pad)` (Y axis inverted for screen coords)
- Draws axes (in light gray) when 0 is within the respective ranges:
  - y‑axis if `xmin ≤ 0 ≤ xmax`
  - x‑axis if `ymin ≤ 0 ≤ ymax`
- Constructs a `Polyline` (`SteelBlue`, thickness 2) by transforming each sampled `(x, y)` to pixel coordinates and adding to `PlotCanvas`.

---

### Example walkthrough: `a=2; b=1; a*x + b`
1) Setup: `a=2`, `b=1` → `symbolTable = { a=2.0; b=1.0 }`
2) Expression: `a*x + b`
3) Sampling over `[-10, 10]` by `0.5`:
   - For each `x`, temporary bind `x`; evaluate `2*x + 1`; collect `(x, y)`.
4) Min/Max y are computed; axes drawn (both visible here); polyline rendered as a straight line with slope 2, intercept 1.

---

### Building and running
- Open `AP_Coursework_GUI.sln` in Rider or Visual Studio.
- Set startup project to `AP_Coursework_GUI` (WPF).
- Build and run. Use the input box to type statements, `Evaluate` for a single result, and `Plot` to render an expression in `x`.

---

### Testing
- See `AP_Coursework_GUI/AP_Coursework_GUI/TESTING_PLOTTING.md` for manual plotting tests, error cases, and backend checks using `ExprEvaluator.ResetState` and `EvaluateExprForX` (can be adapted for unit tests).

---

### Known limitations and design notes
- Exponentiation supports integer exponents only; fractional powers raise a runtime error (no Math library used).
- Single‑parameter functions only (easily extendable to multi‑parameter with updated grammar and call semantics).
- Dynamic typing: variables hold numeric values; integer‑like results display without decimals, others with formatted decimals.
- Division logic: integer‑like operands may follow integer division; otherwise float division is used.

---

### Extending the language
- Static typing (optional): introduce `int`/`float` declarations in the grammar and a type environment.
- Multi‑parameter functions: extend `<functiondef>` and `<primary>` for tuple of identifiers and call lists.
- Additional functions: add built‑ins (sin, cos, etc.) by extending tokens and parser productions.
- Better pretty‑printing: allow user‑configurable numeric formats.

---

### File map: where to look for what
- Grammar and parsing: `AP_Coursework/AP_Coursework/Program.fs` — see `parseE`, `parseT`, `parseF`, `parseU`, `parseP`, and `parseAndEval`.
- Lexer and tokens: same file — `Token` DU and `lexer` function.
- Evaluation helpers: `pow`, `intDiv`, `intMod`, `isIntLike` in `Program.fs`.
- State tables and public API: `symbolTable`, `functionTable`, `EvaluateExpression`, `EvaluateExprForX`, `ResetState` in `Program.fs`.
- GUI integration and plotting: `AP_Coursework_GUI/AP_Coursework_GUI/MainWindow.xaml.cs` — `Evaluate_Click`, `Plot_Click`, `DrawPlot`.
- UI layout: `AP_Coursework_GUI/AP_Coursework_GUI/MainWindow.xaml`.
