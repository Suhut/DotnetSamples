using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NpgsqlTypes;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.PostgreSQL.ColumnWriters;
using TracesAndLogs.Shared.Observability.CusttomColumnWriter;


namespace TracesAndLogs.Shared.Observability;

public static class ObservabilityExtensions
{
    public static void AddTracesAndLogs(this WebApplicationBuilder builder)
    {
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
    }

    public static void AddTracesAndLogs(this WebApplication app)
    {
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<RequestIdMiddleware>();
    }
}