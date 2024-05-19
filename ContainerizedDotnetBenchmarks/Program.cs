using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace ContainerizedDotnetBenchmarks;

class Program
{
    static HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(5) };
    static string _serverPassword;
    static string _serverAddress;
    static string _instanceName;
    static string _dotnetFramework;
    static int _benchmarkTotalCount;
    
    static async Task Main(string[] args)
    {
        if (args.Length < 5) throw new Exception("Not all arguments where provided.");
        if (!args[0].EndsWith("proj")) throw new Exception("Invalid project path. Path with project file name is required.");

        var benchmarkProjectPath = args[0];
        _dotnetFramework = args[1];
        _instanceName = args[2];
        _serverAddress = args[3];
        _serverPassword = args[4];
        
        // start benchmarks
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

            process.OutputDataReceived += SendMessage;

            process.ErrorDataReceived += SendErrorMessage;

            // Start the process and begin asynchronous reading
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            await SendBenchmarkResults();
        }
    }

    static async void SendMessage(object sender, DataReceivedEventArgs eventArgs)
    {
        var consoleMessage = eventArgs.Data ?? string.Empty;
        Console.WriteLine(consoleMessage);
        
        if (consoleMessage.StartsWith("// ***** Found "))
        {
            _benchmarkTotalCount = int.Parse(Regex.Match(consoleMessage, @"\d+").Value);
            
            var initialContent = new Dictionary<string, string>
            {
                { "password", _serverPassword },
                { "instance name", _instanceName },
                { "message", consoleMessage },
                { "remaining benchmarks", _benchmarkTotalCount.ToString() },
                { "estimated finish", string.Empty },
                { "total benchmark count", _benchmarkTotalCount.ToString() },
                { "is error", "false" }
            };
            await _httpClient.PostAsync(_serverAddress + "/status", new FormUrlEncodedContent(initialContent));
            return;
        }
        if (!consoleMessage.StartsWith("// ** Remained ")) return;

        var remainingBenchmarks = Regex.Match(consoleMessage, @"\d+").Value;
        var estFinish = Regex.Match(consoleMessage, @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2})").Value;
        
        
        var content = new Dictionary<string, string>
        {
            { "password", _serverPassword },
            { "instance name", _instanceName },
            { "message", consoleMessage },
            { "remaining benchmarks", remainingBenchmarks },
            { "estimated finish", estFinish },
            { "total benchmark count", _benchmarkTotalCount.ToString() },
            { "is error", "false" }
        };
        await _httpClient.PostAsync(_serverAddress + "/status", new FormUrlEncodedContent(content));
    }

    static async void SendErrorMessage(object sender, DataReceivedEventArgs eventArgs)
    {
        var consoleMessage = eventArgs.Data ?? string.Empty;
        Console.WriteLine(consoleMessage);
        
        var content = new Dictionary<string, string>
        {
            { "password", _serverPassword },
            { "instance name", _instanceName },
            { "message", consoleMessage },
            { "is error", "true"}
        };
        await _httpClient.PostAsync(_serverAddress + "/status", new FormUrlEncodedContent(content));
    }

    static async Task SendBenchmarkResults()
    {
        ZipFile.CreateFromDirectory("BenchmarkDotNet.Artifacts", "BenchmarkResults.zip");
        
        using (var multipartFormContent = new MultipartFormDataContent())
        {
            multipartFormContent.Add(new StringContent(_serverPassword), name: "password");
            multipartFormContent.Add(new StringContent(_instanceName), name: "instance name");
            multipartFormContent.Add(new StreamContent(File.OpenRead("BenchmarkResults.zip")), name: "BenchmarkResults", fileName: "BenchmarkResults.zip");

            await _httpClient.PostAsync(_serverAddress + "/results", multipartFormContent);
        }
    }
}