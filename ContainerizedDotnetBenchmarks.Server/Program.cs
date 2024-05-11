using System.Security.Cryptography;
using System.Text;

namespace ContainerizedDotnetBenchmarks.Server;

public class Program
{
    private static byte[] _serverPassword;
    
    public static void Main(string[] args)
    {
        if (args.Length < 1) _serverPassword = Encoding.Unicode.GetBytes("password12345");
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

        app.MapPost("/status", async (HttpRequest request) =>
            {
                if (!request.HasFormContentType) return Results.BadRequest("Unsupported Media Type");
                
                var form = await request.ReadFormAsync();
                
                if (SHA256.HashData(Encoding.Unicode.GetBytes(form["password"].ToString()))
                    .Zip(_serverPassword, (byteFromRemote, byteFromTruth) => byteFromRemote == byteFromTruth)
                    .All(x => x)) return Results.Unauthorized();

                if (form["is error"] == "false")
                {
                    int remainingBenchmarks;
                    int totalBenchmarks;
                    if (!int.TryParse(form["remaining benchmarks"], out remainingBenchmarks)) return Results.BadRequest();
                    if (!int.TryParse(form["total benchmark count"], out totalBenchmarks)) return Results.BadRequest();
                    
                    Console.WriteLine($"{form["instance name"]}: completed {totalBenchmarks-remainingBenchmarks}/{totalBenchmarks} estimated finish at {form["estimated finish"]}");
                }
                else
                {
                    Console.WriteLine($"ERROR IN INSTANCE {form["instance name"]}: {form["message"]}");
                }
                
                return Results.Ok();
            })
            .WithName("PostStatus")
            .WithOpenApi();

        app.Run();
    }
}