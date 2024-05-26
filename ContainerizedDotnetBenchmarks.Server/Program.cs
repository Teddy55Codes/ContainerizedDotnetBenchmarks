using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using NLog.Config;
using NLog.Web;
using LogLevel = NLog.LogLevel;

namespace ContainerizedDotnetBenchmarks.Server;

public class Program
{
    private static byte[] _serverPassword;
    private static bool _useSpectreConsole;
    
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // set if Spectre.Console should be used.
        _useSpectreConsole = builder.Configuration.GetValue<bool>("useSpectreConsole");
        if (_useSpectreConsole)
        {
            // only show lifetime updates and errors on the console
            NLog.LogManager.Configuration.LoggingRules.First(r => r.Targets.Count == 1 && r.Targets[0].Name == "logconsole").LoggerNamePattern = "Microsoft.Hosting.Lifetime*";
            NLog.LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Error, NLog.LogManager.Configuration.AllTargets.First(t => t.Name == "logconsole")));
            
            builder.Services.AddSingleton<IProgressService, ProgressService>();
        }
        
        builder.Services.AddSingleton<INotificationService, NotificationService>();
        
        // set password
        var authenticationConfig = builder.Configuration.GetSection("AuthenticationConfig").Get<AuthenticationConfig>();
        _serverPassword = SHA256.HashData(authenticationConfig is null ? Encoding.Unicode.GetBytes("password12345") : Encoding.Unicode.GetBytes(authenticationConfig.Password));

        // NLog: Setup NLog for Dependency injection
        builder.Logging.ClearProviders();
        builder.Host.UseNLog();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapGet("/ping", (HttpRequest _) =>
            {
                app.Logger.LogInformation("Received Ping");
                return Results.Ok("pong");
            });
        
        app.MapPost("/status", async (HttpRequest request, [FromServices] IProgressService? progressService) =>
            {
                if (!request.HasFormContentType)
                {
                    app.Logger.LogDebug("Received status request with invalid media type");
                    return Results.BadRequest("Unsupported Media Type");
                }
                
                var form = await request.ReadFormAsync();

                if (!CheckPassword(form["password"].ToString()))
                {
                    app.Logger.LogDebug("Received unauthorized status request");
                    return Results.Unauthorized();
                }

                if (form["is error"] == "false")
                {
                    if (!int.TryParse(form["remaining benchmarks"], out int remainingBenchmarks)) return Results.BadRequest("Invalid \"remaining benchmarks\" provided.");
                    if (!int.TryParse(form["total benchmark count"], out int totalBenchmarks)) return Results.BadRequest("Invalid \"total benchmark count\" provided.");

                    DateTime estimatedFinishTime;
                    if (!DateTime.TryParseExact(
                            form["estimated finish"].ToString(), 
                            "yyyy-MM-dd HH:mm", 
                            CultureInfo.InvariantCulture, 
                            DateTimeStyles.None, 
                            out estimatedFinishTime) && 
                        form["estimated finish"].ToString() != string.Empty)
                    {
                        return Results.BadRequest("Invalid \"estimated finish\" provided. (format is yyyy-MM-dd HH:mm)");
                    }
                    
                    if (!DateTime.TryParseExact(form["current time"].ToString(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime currentClientTime)) return Results.BadRequest("Invalid \"current time\" provided. (format is yyyy-MM-dd HH:mm)");

                    app.Logger.LogInformation($"instance {form["instance name"]} running {form["benchmark project"]}: completed {totalBenchmarks-remainingBenchmarks}/{totalBenchmarks} {(form["estimated finish"].ToString() == string.Empty ? "" : $"estimated finish at {form["estimated finish"]}")}");
                    if (progressService is not null) progressService.UpdateTask(
                        $"Instance: {form["instance name"]} Benchmark: {form["benchmark project"]}", 
                        remainingBenchmarks == 0 ? "Finished" : "Running", 
                        totalBenchmarks-remainingBenchmarks, 
                        totalBenchmarks, 
                        form["estimated finish"].ToString() == string.Empty ? TimeSpan.Zero : estimatedFinishTime - currentClientTime);
                    
                }
                else
                {
                    app.Logger.LogInformation($"ERROR IN INSTANCE {form["instance name"]} while running {form["benchmark project"]}: {form["message"]}");
                }
                
                return Results.Ok();
            })
            .WithName("PostStatus")
            .WithOpenApi();
        
        app.MapPost("/result", async ([FromServices] IProgressService? progressService, INotificationService notificationService, HttpRequest request) =>
            {
                if (!request.HasFormContentType)
                {
                    app.Logger.LogDebug("Received result request with invalid media type");
                    return Results.BadRequest("Unsupported Media Type");
                }
                
                var form = await request.ReadFormAsync();

                if (!CheckPassword(form["password"].ToString()))
                {
                    app.Logger.LogDebug("Received unauthorized result request");
                    return Results.Unauthorized();
                }
                
                if (form.Files["BenchmarkResults"] is { } file)
                {
                    var timeDirectory = DateTime.Now.ToString("yyyy-MM-dd");
                    Directory.CreateDirectory(Path.Combine("BenchmarkResults", form["instance name"].ToString(), form["benchmark project"].ToString(), timeDirectory));
                    
                    var filePath = CheckedSave(Path.Combine("BenchmarkResults", form["instance name"].ToString(), form["benchmark project"].ToString(), timeDirectory, file.FileName));
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    
                    app.Logger.LogInformation($"Received benchmark results for project {form["benchmark project"]} from instance {form["instance name"]}. Results are saved under {filePath}.");
                    var desktopNotificationSuccessful = await notificationService.ShowNotification($"Instance {form["instance name"]} finished.", $"{form["instance name"]} finished project {form["benchmark project"]}. Results are saved under {filePath}.");
                    if (!desktopNotificationSuccessful) app.Logger.LogWarning($"Desktop notification failed while logging result received from instance {form["instance name"]} with project {form["benchmark project"]}");
                    if (progressService is not null) progressService.UpdateTask(
                        $"Instance: {form["instance name"]} Benchmark: {form["benchmark project"]}", 
                        "Uploaded", 
                        null, 
                        null, 
                        TimeSpan.Zero);
                    return Results.Ok();
                }

                app.Logger.LogDebug("Received result request with missing file");
                return Results.BadRequest();
            })
            .WithName("PostResult")
            .WithOpenApi();
        
        app.Run();
    }

    static bool CheckPassword(string password) =>
        SHA256.HashData(Encoding.Unicode.GetBytes(password))
            .Zip(_serverPassword, (byteFromRemote, byteFromTruth) => byteFromRemote == byteFromTruth)
            .All(x => x);

    static string CheckedSave(string filePath)
    {
        if (!File.Exists(filePath)) return filePath;
        
        string newFileName = string.Empty;
        int i = 1;
        var newFilenameFound = false;
        while (!newFilenameFound)
        {
            var splitName = filePath.Split(".");
            newFileName = $"{string.Join(".", splitName[..^1])} {i}.{splitName[^1]}";
            newFilenameFound = !File.Exists(newFileName);
            i++;
        }

        return newFileName;
    }
}