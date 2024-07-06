using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetryTraces;
using System.Data;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql("Host=localhost;Database=coba_tracing;Username=postgres;Password=1234"));

builder.Services.AddOpenTelemetry()
    .WithTracing(options => options 
        .ConfigureResource(resourceBuilder =>
        {
            resourceBuilder.AddService(
                builder.Environment.ApplicationName,
                builder.Environment.EnvironmentName,
                "1.0",
                false,
                Environment.MachineName);
        })
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation() 
        .AddNpgsql()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317");
        })
    );

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

//logging
//tracing 

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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


    return Results.Ok();
}).WithOpenApi();

app.MapGet("/cmdFail", async () =>
{
    var random = new Random();
    int dice = random.Next(0, 5);
    await Task.Delay(dice * 1000);

    return Results.BadRequest();
}).WithOpenApi();

app.MapGet("/EF", async (ApplicationDbContext context) =>
{
    context.MyEntities.Add(new MyEntity { Name=$"Suhut EF {Guid.NewGuid().ToString("N")}"});
    await context.SaveChangesAsync(); 
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
    return Results.Ok();
}).WithOpenApi();

app.Run();


