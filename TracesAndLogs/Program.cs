using Dapper;
using Hangfire;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using OpenTelemetry.Trace;
using Serilog;
using System.Data;
using System.Diagnostics;
using TracesAndLogs;
using TracesAndLogs.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddTracesAndLogs(true);

builder.Services.AddDbContext<PgDbContext>(options =>
        options.UseNpgsql("Host=localhost;Database=coba_tracing;Username=postgres;Password=1234"));

builder.Services.AddDbContext<SqlServerDbContext>(options =>
        options.UseSqlServer("Server=SUHUT-TUF;Database=coba_tracing;TrustServerCertificate=True;Trusted_Connection=True;MultipleActiveResultSets=true;Application Name=TracesAndLogs;"));

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
builder.Services.AddHttpClient("api3", c =>
{
    c.BaseAddress = new Uri("http://localhost:5186");
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.Add(
        "accept", "application/json");

});


builder.Services.AddHangfire(x => x.UseInMemoryStorage());
builder.Services.AddHangfireServer();
builder.Services.AddTransient<MyHangfireService>();


var app = builder.Build();

//logging
//tracing 

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseTracesAndLogs(true);

app.UseHangfireDashboard("/hangfire");


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

        var activityEvent01 = new ActivityEvent("Transaksi Sukses 01", tags: new ActivityTagsCollection { new("products.count", 1), new("products.sum", 2) });
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

app.MapGet("/PG_EF", async (PgDbContext context) =>
{
    context.MyEntities.Add(new PgMyEntity { Name = $"Suhut EF {Guid.NewGuid().ToString("N")}" });
    await context.SaveChangesAsync();


    Log.Information($"dari EF");
    return Results.Ok();
}).WithOpenApi();

app.MapGet("/PG_Dapper", async () =>
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

app.MapGet("/SQLSERVER_EF", async (SqlServerDbContext context) =>
{
    context.MyEntities.Add(new SqlServerMyEntity { Name = $"Suhut EF {Guid.NewGuid().ToString("N")}" });
    await context.SaveChangesAsync();


    Log.Information($"dari EF");
    return Results.Ok();
}).WithOpenApi();

app.MapGet("/SQLSERVER_Dapper", async () =>
{

    using (IDbConnection db = new SqlConnection("Server=SUHUT-TUF;Database=coba_tracing;TrustServerCertificate=True;Trusted_Connection=True;MultipleActiveResultSets=true;Application Name=TracesAndLogs;"))
    {
        db.Open();
        var ssql = @"INSERT INTO ""MyEntities"" (""Name"") VALUES('SUHUT Dapper')";
        await db.ExecuteAsync(ssql);
    }
    Log.Information($"dari Dapper");
    return Results.Ok();
}).WithOpenApi();

app.MapGet("/CallApi2", async (IHttpClientFactory httpClientFactory, IHttpContextAccessor ctx) =>
{

    Log.Information("Calling api2");

    var client = httpClientFactory.CreateClient("api2");  

    var requestId = Activity.Current?.Id;
    if (requestId != null)
    {
        client.DefaultRequestHeaders.AddCorrelationId(ctx.HttpContext!.GetCorrelationId()!);
        client.DefaultRequestHeaders.AddParentRequestId(requestId);
    }


    var response = await client.GetAsync("WeatherForecast");

    if (response.IsSuccessStatusCode)
        return Results.Ok(await response.Content.ReadAsStringAsync());

    return Results.BadRequest();
}).WithOpenApi();

app.MapGet("/CallApi3", async (MyHangfireService service, IHttpContextAccessor ctx) =>
{

    Log.Information("Calling api3");

    BackgroundJob.Enqueue(() => service.MyBackgroundMethod(ctx.HttpContext!.GetCorrelationId()!));

    return Results.Ok();
}).WithOpenApi();

app.Run();


