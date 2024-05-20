using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace ContainerizedDotnetBenchmarks;

partial class Program
{
    private const int maxRequestRetries = 20;
    static HttpClient _httpClient = new();
    static string _serverPassword;
    static string _serverAddress;
    static string _instanceName;

    static string _currentProjectName;
    static int _currentBenchmarkTotalCount;
    
    static async Task Main(string[] args)
    {
        if (args.Length < 5) throw new ArgumentException("Not all arguments where provided.");

        var benchmarkProjectPaths = args[0].Split(";").Select(p => Path.Combine("/BenchmarkProj", p)).ToList();
        if (!benchmarkProjectPaths.All(f => DotnetProjectFile().IsMatch(f))) throw new ArgumentException("One or more invalid project path. Path with project file name is required.");
        if (!benchmarkProjectPaths.All(File.Exists)) throw new FileNotFoundException("One or more of the provided projects where not found.");
        
        var tfmsForBenchmarks = args[1].Split(";");
        if (benchmarkProjectPaths.Count != tfmsForBenchmarks.Length) throw new ArgumentException("Different amount of projects found and target frameworks provided. supply target frameworks for every project seperated with a semicolons.");
        
        _instanceName = args[2];
        _serverAddress = args[3];
        _serverPassword = args[4];
        
        for (int i = 0; i < benchmarkProjectPaths.Count; i++)
        {
            await RunBenchmarkSet(benchmarkProjectPaths[i], tfmsForBenchmarks[i]);
        }
    }

    static async Task RunBenchmarkSet(string projectFilePath, string benchmarkTFM)
    {
        _currentProjectName = string.Join('.', Path.GetFileName(projectFilePath).Split(".")[..^1]);
        var projectDirectory = Path.GetDirectoryName(projectFilePath);
        
        var startInfoRestore = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"restore {projectFilePath}", 
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = new Process())
        {
            process.StartInfo = startInfoRestore;
            process.Start();
            await process.WaitForExitAsync();
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run -c Release --framework {benchmarkTFM} --project {projectFilePath}", 
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using (Process process = new Process())
        {
            process.StartInfo = startInfo;
            process.StartInfo.WorkingDirectory = projectDirectory;

            process.OutputDataReceived += SendMessage;

            process.ErrorDataReceived += SendErrorMessage;

            // Start the process and begin asynchronous reading
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            await SendBenchmarkResults(projectDirectory);
        }
    }

    static async void SendMessage(object sender, DataReceivedEventArgs eventArgs)
    {
        var consoleMessage = eventArgs.Data ?? string.Empty;
        if (Environment.UserInteractive) Console.WriteLine(consoleMessage);
        
        if (consoleMessage.StartsWith("// ***** Found "))
        {
            _currentBenchmarkTotalCount = int.Parse(Regex.Match(consoleMessage, @"\d+").Value);
            
            var initialContent = new Dictionary<string, string>
            {
                { "password", _serverPassword },
                { "instance name", _instanceName },
                { "benchmark project", _currentProjectName },
                { "message", consoleMessage },
                { "remaining benchmarks", _currentBenchmarkTotalCount.ToString() },
                { "estimated finish", string.Empty },
                { "total benchmark count", _currentBenchmarkTotalCount.ToString() },
                { "is error", "false" }
            };
            for (int i = 0; i <= maxRequestRetries; i++)
            {
                try
                {
                    await _httpClient.PostAsync(_serverAddress + "/status", new FormUrlEncodedContent(initialContent));
                    break;
                }
                catch (HttpRequestException)
                {
                    if (Environment.UserInteractive) Console.WriteLine($"http request failed on try {i+1}/{maxRequestRetries}");
                }
            }
            return;
        }
        if (!consoleMessage.StartsWith("// ** Remained ")) return;

        var remainingBenchmarks = Regex.Match(consoleMessage, @"\d+").Value;
        var estFinish = Regex.Match(consoleMessage, @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2})").Value;
        
        
        var content = new Dictionary<string, string>
        {
            { "password", _serverPassword },
            { "instance name", _instanceName },
            { "benchmark project", _currentProjectName },
            { "message", consoleMessage },
            { "remaining benchmarks", remainingBenchmarks },
            { "estimated finish", estFinish },
            { "total benchmark count", _currentBenchmarkTotalCount.ToString() },
            { "is error", "false" }
        };
        
        for (int i = 0; i <= maxRequestRetries; i++)
        {
            try
            {
                await _httpClient.PostAsync(_serverAddress + "/status", new FormUrlEncodedContent(content));
                break;
            }
            catch (HttpRequestException)
            {
                if (Environment.UserInteractive) Console.WriteLine($"http request failed on try {i+1}/{maxRequestRetries}");
            }
        }
    }

    static async void SendErrorMessage(object sender, DataReceivedEventArgs eventArgs)
    {
        var consoleMessage = eventArgs.Data ?? string.Empty;
        if (Environment.UserInteractive) Console.WriteLine(consoleMessage);
        
        var content = new Dictionary<string, string>
        {
            { "password", _serverPassword },
            { "instance name", _instanceName },
            { "benchmark project", _currentProjectName },
            { "message", consoleMessage },
            { "is error", "true"}
        };

        for (int i = 0; i <= maxRequestRetries; i++)
        {
            try
            {
                await _httpClient.PostAsync(_serverAddress + "/status", new FormUrlEncodedContent(content));
                break;
            }
            catch (HttpRequestException)
            {
                if (Environment.UserInteractive) Console.WriteLine($"http request failed on try {i+1}/{maxRequestRetries}");
            }
        }
    }

    static async Task SendBenchmarkResults(string projectDirectory)
    {
        var zipFilePath = Path.Combine(projectDirectory, "BenchmarkResults.zip");
        ZipFile.CreateFromDirectory(Path.Combine(projectDirectory, "BenchmarkDotNet.Artifacts"), zipFilePath);
        
        using (var multipartFormContent = new MultipartFormDataContent())
        {
            multipartFormContent.Add(new StringContent(_serverPassword), name: "password");
            multipartFormContent.Add(new StringContent(_instanceName), name: "instance name");
            multipartFormContent.Add(new StringContent(_currentProjectName), name: "benchmark project");
            multipartFormContent.Add(new StreamContent(File.OpenRead(zipFilePath)), name: "BenchmarkResults", fileName: "BenchmarkResults.zip");
            
            for (int i = 0; i <= maxRequestRetries; i++)
            {
                try
                {
                    await _httpClient.PostAsync(_serverAddress + "/result", multipartFormContent);
                    break;
                }
                catch (HttpRequestException)
                {
                    if (Environment.UserInteractive) Console.WriteLine($"http request failed on try {i+1}/{maxRequestRetries}");
                }
            }
        }
        
        File.Delete(zipFilePath);
    }

    [GeneratedRegex(@"\..+proj")]
    private static partial Regex DotnetProjectFile();
}