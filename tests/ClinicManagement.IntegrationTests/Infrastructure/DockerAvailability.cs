using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace ClinicManagement.IntegrationTests.Infrastructure;

public static class DockerAvailability
{
    private const string SkipReason =
        "Docker is not available on this machine, so the PostgreSQL test container cannot start.";

    private const int ConnectTimeoutMilliseconds = 750;

    static DockerAvailability()
    {
        IsAvailable = Detect();
    }

    public static bool IsAvailable { get; }

    public static string Reason => SkipReason;

    private static bool Detect()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return true;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return CanConnectToWindowsDockerPipe();
        }

        return File.Exists("/var/run/docker.sock") || File.Exists("/run/docker.sock");
    }

    private static bool CanConnectToWindowsDockerPipe()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", "docker_engine", PipeDirection.InOut);
            pipe.Connect(ConnectTimeoutMilliseconds);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}