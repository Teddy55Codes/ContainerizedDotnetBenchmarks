using System.Collections.Concurrent;
using Spectre.Console;

namespace ContainerizedDotnetBenchmarks.Server;

public delegate void TaskUpdate(string taskName, string description, double? currentValue, double? maxValue, TimeSpan remainingTime);

public class ProgressService : IProgressService
{
    private ConcurrentDictionary<string, ProgressTask> _activeClients = new ConcurrentDictionary<string, ProgressTask>();
    private event TaskUpdate OnUpdateTask;
    
    public ProgressService()
    {
        AnsiConsole.Progress()
            .Columns([
                new TaskDescriptionColumn(),    // Task description
                new ProgressBarColumn(),         // Progress bar
                new PercentageColumn()         // Percentage
            ])
            .StartAsync(async ctx =>
            {
                // Define tasks
                OnUpdateTask += (name, description, value, maxValue, timeRemaining) =>
                {
                    var completeDescription = $"{name} | {description} | {(value is null || maxValue is null ? "NA" : $"{value}/{maxValue}")} | {(timeRemaining == TimeSpan.Zero ? "NA" : timeRemaining.ToString("g"))}";
                    var task = _activeClients.GetOrAdd(name, s => ctx.AddTask(completeDescription));
                    task.Description = completeDescription;
                    if (value is not null) task.Value = (double)value;
                    if (maxValue is not null) task.MaxValue = (double)maxValue;
                };
                
                while (true)
                {
                    // Simulate some work
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            });
    }

    public void UpdateTask(string taskName, string description, double? currentValue, double? maxValue, TimeSpan remainingTime) => 
        OnUpdateTask?.Invoke(taskName, description, currentValue, maxValue, remainingTime);
}