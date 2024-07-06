using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog.Sinks.PostgreSQL.ColumnWriters;
using System.Data;
using System.Diagnostics;
using TracesAndLogs;
using TracesAndLogs.CusttomColumnWriter;

using NpgsqlTypes;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.PostgreSQL.ColumnWriters;
using Serilog.Ui.Core.OptionsBuilder; 
using Serilog.Ui.PostgreSqlProvider.Extensions;
using Serilog.Ui.PostgreSqlProvider.Models;
using Serilog.Ui.Web.Extensions; 

var builder = WebApplication.CreateBuilder(args);

IDictionary<string, ColumnWriterBase> columnWriters = new Dictionary<string, ColumnWriterBase>
{
    { "Id", new IdAutoIncrementColumnWriter () },
    { "Message", new RenderedMessageColumnWriter() },
    { "MessageTemplate", new MessageTemplateColumnWriter() },
    { "Level", new LevelColumnWriter() },
    { "LevelName", new LevelColumnWriter(true, NpgsqlDbType.Text) },
    { "Timestamp", new TimestampColumnWriter() },
    { "Exception", new ExceptionColumnWriter() },
    { "LogEvent", new LogEventSerializedColumnWriter() },
    //{ "Properties", new PropertiesColumnWriter(NpgsqlDbType.Text) },  
    { "SpanId", new SpanIdColumnWriterBase() },
    { "TranceId", new TranceIdColumnWriterBase() },
    { "RequestId", new SinglePropertyColumnWriter("RequestId", format: "l") },

};

builder.Host
    .UseSerilog((context, config) =>
    {
        config.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)


            .WriteTo.PostgreSQL("User ID=postgres;Password=1234;Host=localhost;Port=5432;Database=serilog;",
                                    "logs",
                                    columnOptions: columnWriters,
                                    needAutoCreateTable: true

            )
            ;

        if (context.HostingEnvironment.IsDevelopment())
            config.WriteTo.Console(new RenderedCompactJsonFormatter());
    });


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


app.UseMiddleware<RequestLoggingMiddleware>();

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

app.Run();


