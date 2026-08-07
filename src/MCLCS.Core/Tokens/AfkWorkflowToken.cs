using System.Text;

namespace MCLCS.Core.Tokens;

/// <summary>挂机工作流指令类型。</summary>
public enum AfkOpKind
{
    /// <summary>F 功能键：<c>F&lt;n&gt;</c>，n ∈ [1,24]。例：F10。</summary>
    FunctionKey,
    /// <summary>延迟等待：<c>D&lt;秒&gt;</c>。例：D4 = 等待 4 秒。</summary>
    Delay,
    /// <summary>长按：<c>L&lt;秒&gt;</c>，作用于上一条按键指令。例：L3 = 长按 3 秒。</summary>
    LongPress,
    /// <summary>虚拟键码：<c>K&lt;code&gt;</c>，code ∈ [1,254]。例：K39 = 键码 39。</summary>
    KeyCode,
    /// <summary>连点：<c>C&lt;次数&gt;-&lt;间隔毫秒&gt;</c>。例：C1-500 = 每 500ms 点 1 次。</summary>
    Click,
    /// <summary>整体重复：<c>*&lt;次数&gt;</c>，0 表示无限循环。例：*0。</summary>
    Repeat
}

/// <summary>单条挂机指令。</summary>
public sealed class AfkInstruction
{
    public AfkInstruction(AfkOpKind kind, int a, int b = 0)
    {
        Kind = kind;
        A = a;
        B = b;
    }

    public AfkOpKind Kind { get; }

    /// <summary>主参数（键号 / 秒数 / 键码 / 次数）。</summary>
    public int A { get; }

    /// <summary>次参数（仅连点使用：间隔毫秒）。</summary>
    public int B { get; }

    /// <summary>序列化回 Token 片段。</summary>
    public string ToTokenPart() => Kind switch
    {
        AfkOpKind.FunctionKey => $"F{A}",
        AfkOpKind.Delay => $"D{A}",
        AfkOpKind.LongPress => $"L{A}",
        AfkOpKind.KeyCode => $"K{A}",
        AfkOpKind.Click => $"C{A}-{B}",
        AfkOpKind.Repeat => $"*{A}",
        _ => ""
    };

    /// <summary>中文可读描述（用于界面展示工作流步骤）。</summary>
    public string Describe() => Kind switch
    {
        AfkOpKind.FunctionKey => $"按下功能键 F{A}",
        AfkOpKind.Delay => $"等待 {A} 秒",
        AfkOpKind.LongPress => $"长按 {A} 秒",
        AfkOpKind.KeyCode => $"按下按键（键码 {A}）",
        AfkOpKind.Click => B > 0 ? $"连点 {A} 次，间隔 {B} 毫秒" : $"点击 {A} 次",
        AfkOpKind.Repeat => A == 0 ? "整体无限循环" : $"整体重复 {A} 次",
        _ => "未知指令"
    };

    public override string ToString() => ToTokenPart();
}

/// <summary>解析结果。</summary>
public sealed class AfkParseResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public List<AfkInstruction> Instructions { get; init; } = new();

    /// <summary>整体重复次数；0 = 无限循环；未指定时为 1。</summary>
    public int RepeatCount { get; init; } = 1;

    public bool IsInfinite => Ok && Instructions.Any(i => i.Kind == AfkOpKind.Repeat) && RepeatCount == 0;

    /// <summary>不含 <c>*</c> 的动作指令。</summary>
    public IEnumerable<AfkInstruction> Actions => Instructions.Where(i => i.Kind != AfkOpKind.Repeat);

    public static AfkParseResult Fail(string error) => new() { Ok = false, Error = error };
}

/// <summary>
/// 挂机工作流 Token 解析器。
/// <para>格式：分号分隔的指令序列，例 <c>F10;D4;L3;K39;C1-500;*0</c></para>
/// <list type="bullet">
/// <item><c>F10</c> — 按下 F10</item>
/// <item><c>D4</c> — 等待 4 秒</item>
/// <item><c>L3</c> — 长按 3 秒</item>
/// <item><c>K39</c> — 按下键码 39</item>
/// <item><c>C1-500</c> — 连点 1 次，间隔 500 毫秒</item>
/// <item><c>*0</c> — 整体无限循环（放在末尾）</item>
/// </list>
/// 大小写不敏感，允许空白；<c>*</c> 只能出现一次且必须在最后。
/// </summary>
public static class AfkWorkflowToken
{
    public const int MaxFunctionKey = 24;
    public const int MaxDelaySeconds = 86400;
    public const int MaxLongPressSeconds = 3600;
    public const int MaxKeyCode = 254;
    public const int MaxClickCount = 10000;
    public const int MinClickIntervalMs = 10;
    public const int MaxClickIntervalMs = 600000;
    public const int MaxRepeat = 9999;
    public const int MaxInstructions = 128;

    /// <summary>规格示例 Token。</summary>
    public const string Sample = "F10;D4;L3;K39;C1-500;*0";

    public static AfkParseResult Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return AfkParseResult.Fail("Token 为空");

        var parts = token.Split(';', StringSplitOptions.RemoveEmptyEntries)
                         .Select(p => p.Trim())
                         .Where(p => p.Length > 0)
                         .ToList();
        if (parts.Count == 0) return AfkParseResult.Fail("Token 不含任何指令");
        if (parts.Count > MaxInstructions) return AfkParseResult.Fail($"指令过多（上限 {MaxInstructions}）");

        var list = new List<AfkInstruction>();
        var repeat = 1;
        var repeatSeen = false;

        for (var i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            var head = char.ToUpperInvariant(p[0]);
            var body = p[1..].Trim();

            if (repeatSeen)
                return AfkParseResult.Fail("重复指令 * 必须位于末尾");

            switch (head)
            {
                case 'F':
                    if (!TryInt(body, 1, MaxFunctionKey, out var f))
                        return AfkParseResult.Fail($"第 {i + 1} 段 '{p}' 非法：F 后应为 1-{MaxFunctionKey}");
                    list.Add(new AfkInstruction(AfkOpKind.FunctionKey, f));
                    break;

                case 'D':
                    if (!TryInt(body, 0, MaxDelaySeconds, out var d))
                        return AfkParseResult.Fail($"第 {i + 1} 段 '{p}' 非法：D 后应为 0-{MaxDelaySeconds} 秒");
                    list.Add(new AfkInstruction(AfkOpKind.Delay, d));
                    break;

                case 'L':
                    if (!TryInt(body, 1, MaxLongPressSeconds, out var l))
                        return AfkParseResult.Fail($"第 {i + 1} 段 '{p}' 非法：L 后应为 1-{MaxLongPressSeconds} 秒");
                    if (list.Count == 0 || (list[^1].Kind != AfkOpKind.FunctionKey && list[^1].Kind != AfkOpKind.KeyCode))
                        return AfkParseResult.Fail($"第 {i + 1} 段 '{p}' 非法：长按必须紧跟在按键指令之后");
                    list.Add(new AfkInstruction(AfkOpKind.LongPress, l));
                    break;

                case 'K':
                    if (!TryInt(body, 1, MaxKeyCode, out var k))
                        return AfkParseResult.Fail($"第 {i + 1} 段 '{p}' 非法：K 后应为 1-{MaxKeyCode}");
                    list.Add(new AfkInstruction(AfkOpKind.KeyCode, k));
                    break;

                case 'C':
                {
                    var seg = body.Split('-', StringSplitOptions.RemoveEmptyEntries);
                    if (seg.Length != 2)
                        return AfkParseResult.Fail($"第 {i + 1} 段 '{p}' 非法：应为 C<次数>-<间隔毫秒>");
                    if (!TryInt(seg[0].Trim(), 1, MaxClickCount, out var cnt))
                        return AfkParseResult.Fail($"第 {i + 1} 段 '{p}' 非法：次数应为 1-{MaxClickCount}");
                    if (!TryInt(seg[1].Trim(), MinClickIntervalMs, MaxClickIntervalMs, out var iv))
                        return AfkParseResult.Fail($"第 {i + 1} 段 '{p}' 非法：间隔应为 {MinClickIntervalMs}-{MaxClickIntervalMs} 毫秒");
                    list.Add(new AfkInstruction(AfkOpKind.Click, cnt, iv));
                    break;
                }

                case '*':
                    if (!TryInt(body, 0, MaxRepeat, out var r))
                        return AfkParseResult.Fail($"第 {i + 1} 段 '{p}' 非法：* 后应为 0-{MaxRepeat}（0=无限）");
                    list.Add(new AfkInstruction(AfkOpKind.Repeat, r));
                    repeat = r;
                    repeatSeen = true;
                    break;

                default:
                    return AfkParseResult.Fail($"第 {i + 1} 段 '{p}' 非法：未知指令 '{head}'");
            }
        }

        if (list.All(i => i.Kind == AfkOpKind.Repeat))
            return AfkParseResult.Fail("Token 不含任何动作指令");

        return new AfkParseResult { Ok = true, Instructions = list, RepeatCount = repeat };
    }

    /// <summary>是否为合法 Token。</summary>
    public static bool IsValid(string? token) => Parse(token).Ok;

    /// <summary>把指令序列序列化回 Token 字符串。</summary>
    public static string Serialize(IEnumerable<AfkInstruction> instructions) =>
        string.Join(";", instructions.Select(i => i.ToTokenPart()));

    /// <summary>生成中文步骤说明（每行一步）。</summary>
    public static string Describe(string? token)
    {
        var r = Parse(token);
        if (!r.Ok) return $"无效 Token：{r.Error}";

        var sb = new StringBuilder();
        var n = 1;
        foreach (var ins in r.Actions)
            sb.AppendLine($"{n++}. {ins.Describe()}");

        sb.Append(r.IsInfinite
            ? "循环：无限（手动停止）"
            : $"循环：{r.RepeatCount} 轮");
        return sb.ToString();
    }

    /// <summary>
    /// 估算单轮耗时（毫秒）。长按/延迟按秒计，连点按 次数×间隔 计，单次按键按 50ms 计。
    /// </summary>
    public static long EstimateCycleMs(string? token)
    {
        var r = Parse(token);
        if (!r.Ok) return 0;

        long total = 0;
        foreach (var ins in r.Actions)
        {
            total += ins.Kind switch
            {
                AfkOpKind.Delay => ins.A * 1000L,
                AfkOpKind.LongPress => ins.A * 1000L,
                AfkOpKind.Click => (long)ins.A * ins.B,
                AfkOpKind.FunctionKey => 50,
                AfkOpKind.KeyCode => 50,
                _ => 0
            };
        }
        return total;
    }

    /// <summary>
    /// 展开为可执行动作序列。无限循环时按 <paramref name="maxCycles"/> 截断，避免界面预览卡死。
    /// </summary>
    public static List<AfkInstruction> Expand(string? token, int maxCycles = 3)
    {
        var r = Parse(token);
        if (!r.Ok) return new List<AfkInstruction>();

        var cycles = r.IsInfinite ? Math.Max(1, maxCycles) : Math.Max(1, Math.Min(r.RepeatCount, maxCycles));
        var actions = r.Actions.ToList();
        var result = new List<AfkInstruction>(actions.Count * cycles);
        for (var c = 0; c < cycles; c++) result.AddRange(actions);
        return result;
    }

    private static bool TryInt(string s, int min, int max, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (!int.TryParse(s, out var v)) return false;
        if (v < min || v > max) return false;
        value = v;
        return true;
    }
}
