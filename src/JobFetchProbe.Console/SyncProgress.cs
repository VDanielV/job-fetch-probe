namespace JobFetchProbe.Console;

/// <summary>Reports on the calling thread, so lines print in order (Progress&lt;T&gt; would post to the thread pool).</summary>
internal sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
