using Xunit;

namespace ClinicManagement.IntegrationTests.Infrastructure;

public sealed class RequiresDockerFactAttribute : FactAttribute
{
    public RequiresDockerFactAttribute()
    {
        if (!DockerAvailability.IsAvailable)
        {
            Skip = DockerAvailability.Reason;
        }
    }
}

public sealed class RequiresDockerTheoryAttribute : TheoryAttribute
{
    public RequiresDockerTheoryAttribute()
    {
        if (!DockerAvailability.IsAvailable)
        {
            Skip = DockerAvailability.Reason;
        }
    }
}