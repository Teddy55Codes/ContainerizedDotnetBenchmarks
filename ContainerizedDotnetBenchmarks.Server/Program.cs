using System.Security.Cryptography;
using System.Text;

namespace ContainerizedDotnetBenchmarks.Server;

public class Program
{
    private static byte[] _serverPassword;
    
    public static void Main(string[] args)
    {
        if (args.Length < 1) _serverPassword = SHA256.HashData(Encoding.Unicode.GetBytes("password12345"));
        else
        {
            _serverPassword = SHA256.HashData(Encoding.Unicode.GetBytes(args[0]));
            args = args[1..];
        }
        
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapGet("/ping", (HttpRequest _) => Results.Ok("pong"));
        
        app.MapPost("/status", async (HttpRequest request) =>
            {
                if (!request.HasFormContentType) return Results.BadRequest("Unsupported Media Type");
                
                var form = await request.ReadFormAsync();

                if (!CheckPassword(form["password"].ToString())) return Results.Unauthorized();

                if (form["is error"] == "false")
                {
                    int remainingBenchmarks;
                    int totalBenchmarks;
                    if (!int.TryParse(form["remaining benchmarks"], out remainingBenchmarks)) return Results.BadRequest();
                    if (!int.TryParse(form["total benchmark count"], out totalBenchmarks)) return Results.BadRequest();
                    
                    Console.WriteLine($"instance {form["instance name"]} running {form["benchmark project"]}: completed {totalBenchmarks-remainingBenchmarks}/{totalBenchmarks} {(form["estimated finish"].ToString() == string.Empty ? "" : $"estimated finish at {form["estimated finish"]}")}");
                }
                else
                {
                    Console.WriteLine($"ERROR IN INSTANCE {form["instance name"]} while running {form["benchmark project"]}: {form["message"]}");
                }
                
                return Results.Ok();
            })
            .WithName("PostStatus")
            .WithOpenApi();
        
        app.MapPost("/result", async (HttpRequest request) =>
            {
                if (!request.HasFormContentType) return Results.BadRequest("Unsupported Media Type");
                
                var form = await request.ReadFormAsync();

                if (!CheckPassword(form["password"].ToString())) return Results.Unauthorized();
                
                if (form.Files["BenchmarkResults"] is { } file)
                {
                    var currentTime = DateTime.Now;
                    var timeDirectory = $"{currentTime.Year}-{currentTime.Month}-{currentTime.Day}";
                    Directory.CreateDirectory(Path.Combine("BenchmarkResults", form["instance name"].ToString(), form["benchmark project"].ToString(), timeDirectory));
                    
                    var filePath = CheckedSave(Path.Combine("BenchmarkResults", form["instance name"].ToString(), form["benchmark project"].ToString(), timeDirectory, file.FileName));
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    
                    Console.WriteLine($"Received benchmark results for project {form["benchmark project"]} from instance {form["instance name"]}. Results are saved under {filePath}.");
                    return Results.Ok();
                }
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