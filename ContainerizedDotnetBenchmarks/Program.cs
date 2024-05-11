using System.Diagnostics;

namespace ContainerizedDotnetBenchmarks;

class Program
{
    static HttpClient _httpClient = new();
    static string _serverPassword;
    static string _serverAddress;
    static string _instanceName;
    static string _dotnetFramework;
    
    static async Task Main(string[] args)
    {
        Console.WriteLine(string.Join(", ", args));
        if (args.Length < 5) throw new Exception("Not all arguments where provided.");
        if (!args[0].EndsWith("proj")) throw new Exception("Invalid project path. Path with project file name is required.");

        var benchmarkProjectPath = args[0];
        _dotnetFramework = args[1];
        _instanceName = args[2];
        _serverAddress = args[3];
        _serverPassword = args[4];
        
        
        // Set up the process start information
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run -c Release --framework {_dotnetFramework} --project {Path.Combine("/BenchmarkProj", benchmarkProjectPath)}", 
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using (Process process = new Process())
        {
            process.StartInfo = startInfo;

            process.OutputDataReceived += SendNonErrorMessage;

            process.ErrorDataReceived += SendErrorMessage;

            // Start the process and begin asynchronous reading
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
        }
    }

    static async void SendNonErrorMessage(object sender, DataReceivedEventArgs eventArgs) => await SendMessage(eventArgs, false);
    
    static async void SendErrorMessage(object sender, DataReceivedEventArgs eventArgs) => await SendMessage(eventArgs, true);

    static async Task SendMessage(DataReceivedEventArgs eventArgs, bool isError)
    {
        var consoleMessage = eventArgs.Data ?? string.Empty;
        if (!consoleMessage.StartsWith("// ** Remained ")) return;
        
        var content = new Dictionary<string, string>
        {
            { "password", _serverPassword },
            { "instance name", _instanceName },
            { "message", consoleMessage },
            { "is error", isError ? "true" : "false" }
        };
        await _httpClient.PostAsync(_serverAddress + "/status", new FormUrlEncodedContent(content));
    }
}