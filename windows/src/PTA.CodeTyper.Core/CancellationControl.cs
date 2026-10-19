// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

namespace PTA.CodeTyper.Core;

/// <summary>
/// 控制键入流程的取消与异步等待。
/// </summary>
public sealed class CancellationControl : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly bool _ownsCts;

    /// <summary>
    /// 获取与此控制对象关联的取消令牌。
    /// </summary>
    public CancellationToken Token => _cts.Token;

    /// <summary>
    /// 获取是否已请求取消。
    /// </summary>
    public bool IsCancellationRequested => _cts.Token.IsCancellationRequested;

    public CancellationControl(CancellationTokenSource? cts = null)
    {
        if (cts != null)
        {
            _cts = cts;
            _ownsCts = false;
        }
        else
        {
            _cts = new CancellationTokenSource();
            _ownsCts = true;
        }
    }

    /// <summary>
    /// 检查是否已收到取消请求。若已取消，则抛出 <see cref="CancelledException"/>。
    /// </summary>
    /// <exception cref="CancelledException">当已请求取消时抛出。</exception>
    public void Check()
    {
        if (_cts.Token.IsCancellationRequested)
        {
            throw new CancelledException("已停止输入。编辑框中可能只有部分代码，请检查后再操作。");
        }
    }

    /// <summary>
    /// 异步等待指定的秒数，期间实时响应取消请求。
    /// </summary>
    /// <param name="seconds">等待时间（秒）。</param>
    public async Task WaitAsync(double seconds)
    {
        if (seconds <= 0)
        {
            Check();
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds), _cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Check();
        }
    }

    /// <summary>
    /// 请求取消当前键入操作。
    /// </summary>
    public void Cancel() => _cts.Cancel();

    public void Dispose()
    {
        if (_ownsCts)
        {
            _cts.Dispose();
        }
    }
}
