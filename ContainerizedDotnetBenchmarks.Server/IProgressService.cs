namespace ContainerizedDotnetBenchmarks.Server;

public interface IProgressService
{
    public void UpdateTask(string taskName, string description, double? currentValue, double? maxValue, TimeSpan remainingTime);
}