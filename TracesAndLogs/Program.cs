using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Trace; 
using Serilog;
using Serilog.Ui.Core.OptionsBuilder;
using Serilog.Ui.PostgreSqlProvider.Extensions;
using Serilog.Ui.PostgreSqlProvider.Models;
using Serilog.Ui.Web.Extensions;
using System.Data;
using System.Diagnostics;
using System.Net.Http;
using TracesAndLogs;
using TracesAndLogs.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddTracesAndLogs(); 

builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql("Host=localhost;Database=coba_tracing;Username=postgres;Password=1234")); 

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient("api2", c =>
{
    c.BaseAddress = new Uri("http://localhost:5253");
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.Add(
        "accept", "application/json");
});

//version : 3.* 
builder.Services.AddSerilogUi(options => options
    .UseNpgSql(optionsDb => optionsDb.WithConnectionString("User ID=postgres;Password=1234;Host=localhost;Port=5432;Database=serilog;")
                                .WithTable("logs")
                                .WithSinkType(PostgreSqlSinkType.SerilogSinksPostgreSQLAlternative)
                                ) 
    );


var app = builder.Build();

//logging
//tracing 

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


//app.UseMiddleware<RequestLoggingMiddleware>();
app.AddTracesAndLogs();

app.UseSerilogUi();//serilog-ui  

app.MapGet("/cmdSuccess", async (TracerProvider tracerProvider, ILogger<Program> logger) =>
{
    var random = new Random();
    int dice = random.Next(0, 5);
    await Task.Delay(dice * 1000);


    var currentActivity = Activity.Current;
    if (currentActivity != null)
    {
        var test01 = currentActivity.AddTag("custom-tag", "halo saya suhut");
        test01.AddBaggage("custom-baggage", "halo saya baggage");

        var activityEvent01 = new ActivityEvent("Transaksi Sukses 01",  tags: new ActivityTagsCollection { new("products.count", 1), new("products.sum", 2) });
        currentActivity.AddEvent(activityEvent01);
        var activityEvent02 = new ActivityEvent("Transaksi Sukses 02", tags: new ActivityTagsCollection { new("products.count", 10), new("products.sum", 20) });
        currentActivity.AddEvent(activityEvent02);
    }

    Log.Information($"dari cmdSuccess");

    return Results.Ok();
}).WithOpenApi();

app.MapGet("/cmdFail", async () =>
{
    var random = new Random();
    int dice = random.Next(0, 5);
    await Task.Delay(dice * 1000);


    Log.Information($"dari cmdFail");

    return Results.BadRequest();
}).WithOpenApi();

app.MapGet("/EF", async (ApplicationDbContext context) =>
{
    context.MyEntities.Add(new MyEntity { Name=$"Suhut EF {Guid.NewGuid().ToString("N")}"});
    await context.SaveChangesAsync();


    Log.Information($"dari EF");
    return Results.Ok();
}).WithOpenApi();

app.MapGet("/Dapper", async () =>
{
  
    using (IDbConnection db = new NpgsqlConnection("Host=localhost;Database=coba_tracing;Username=postgres;Password=1234"))
    {
        db.Open();
        var ssql = @"INSERT INTO ""MyEntities"" (""Name"") VALUES('SUHUT Dapper')";
        await db.ExecuteAsync(ssql);
    }
    Log.Information($"dari Dapper");
    return Results.Ok();
}).WithOpenApi();

app.MapGet("/CallApi2", async (IHttpClientFactory httpClientFactory) =>
{
    var requestId = Activity.Current?.Id;

    Log.Information("Calling api2");

    var client = httpClientFactory.CreateClient("api2");
    client.DefaultRequestHeaders.Add("Request-Id", requestId);

    var response = await client.GetAsync("WeatherForecast");

    if (response.IsSuccessStatusCode)
        return Results.Ok(await response.Content.ReadAsStringAsync());

    return Results.BadRequest();
}).WithOpenApi();

app.Run();


