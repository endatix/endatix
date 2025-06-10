using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Endatix.Framework.Tooling;

public sealed class MeasureExecution : IDisposable
{
#if DEBUG
    private readonly string _className;
    private readonly string _methodName;
    private readonly long _startTime;
    private readonly long _memoryBefore;

    public MeasureExecution(
        [CallerMemberName] string methodName = "",
        [CallerFilePath] string filePath = "")
    {
        _className = Path.GetFileNameWithoutExtension(filePath);
        _methodName = methodName;
        _startTime = Stopwatch.GetTimestamp();
        _memoryBefore = GC.GetTotalMemory(true);
    }

    public void Dispose()
    {
        var memoryAfter = GC.GetTotalMemory(true);
        var elapsedTime = Stopwatch.GetElapsedTime(_startTime);
        Console.WriteLine($"⌚ {_className}.{_methodName} completed in {elapsedTime.TotalMilliseconds}ms | memory allocated: {(memoryAfter - _memoryBefore) / (1024.0 * 1024.0):F2} MB");
    }
#else
    // Empty implementation for production builds
    public MeasureExecution(
        [CallerMemberName] string methodName = "",
        [CallerFilePath] string filePath = "")
    {
        // No-op in production
    }

    public void Dispose()
    {
        // No-op in production
    }
#endif
}