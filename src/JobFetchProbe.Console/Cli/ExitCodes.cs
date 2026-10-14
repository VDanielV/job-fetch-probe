using JobFetchProbe.Application.Results;

namespace JobFetchProbe.Console.Cli;

/// <summary>Process exit codes from the spec.</summary>
public static class ExitCodes
{
    public const int Ok = 0;
    public const int InvalidArguments = 2;
    public const int Stopped = 3;
    public const int TransportFailure = 4;

    public static int From(ProbeStatus status) => status switch
    {
        ProbeStatus.Completed => Ok,
        ProbeStatus.Stopped => Stopped,
        ProbeStatus.TransportFailed => TransportFailure,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
