// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

namespace PTA.CodeTyper.Core;

/// <summary>
/// 键入执行参数选项。
/// </summary>
public sealed record Options
{
    /// <summary>
    /// 单字符键入间隔延迟（秒），范围 0.02 ~ 1.0。
    /// </summary>
    public double Delay { get; init; } = 0.045;

    /// <summary>
    /// 倒计时准备时间（秒），范围 3 ~ 30。
    /// </summary>
    public int Countdown { get; init; } = 7;

    /// <summary>
    /// 每隔多少行校验一次输入，范围 1 ~ 20。
    /// </summary>
    public int CheckEvery { get; init; } = 1;

    /// <summary>
    /// 校验失败后的降速重试次数，范围 0 ~ 2。
    /// </summary>
    public int Retries { get; init; } = 1;

    /// <summary>
    /// 目标浏览器标识，如 "auto"、"edge"、"chrome" 等。
    /// </summary>
    public string Browser { get; init; } = "auto";

    /// <summary>
    /// 校验参数取值是否在有效范围内。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">当 Delay 或 Countdown 超出范围时抛出。</exception>
    /// <exception cref="ArgumentException">当 CheckEvery 或 Retries 超出范围时抛出。</exception>
    public void Validate()
    {
        if (Delay < 0.02 || Delay > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(Delay), "字符间隔必须为 0.02～1 秒。");
        }
        if (Countdown < 3 || Countdown > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(Countdown), "倒计时必须为 3～30 秒。");
        }
        if (CheckEvery < 1 || CheckEvery > 20 || Retries < 0 || Retries > 2)
        {
            throw new ArgumentException("校验间隔或重试次数无效。");
        }
    }
}
