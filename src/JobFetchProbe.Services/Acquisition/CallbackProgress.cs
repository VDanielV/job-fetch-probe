namespace JobFetchProbe.Services.Acquisition;

/// <summary>Reports on the calling thread, so the observer hears things in order (Progress&lt;T&gt; would post to the thread pool).</summary>
internal sealed class CallbackProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value)
    {
        report(value);
    }
}
