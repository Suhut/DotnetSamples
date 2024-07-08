using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NpgsqlTypes;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.PostgreSQL.ColumnWriters;
using Serilog.Ui.Core.OptionsBuilder;
using Serilog.Ui.PostgreSqlProvider.Extensions;
using Serilog.Ui.PostgreSqlProvider.Models;
using Serilog.Ui.Web.Extensions;
using Serilog.Ui.Web.Models;
using System.Diagnostics;
using System.Net.Http.Headers;
using TracesAndLogs.Shared.Observability.CusttomColumnWriter;

namespace TracesAndLogs.Shared.Observability;

public static class ObservabilityExtensions
{

    public const string CorrelationIdKey = "x-correlation-id";
    public const string ParentRequestIdKey = "x-parent-request-id";

    public static void AddTracesAndLogs(this WebApplicationBuilder builder, bool serilogUi)
    {
        var configuration = builder.Configuration;

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
            { "CorrelationId", new SinglePropertyColumnWriter("CorrelationId") },
            { "MachineName", new SinglePropertyColumnWriter("MachineName") }

        };

        builder.Host
            .UseSerilog((context, config) =>
            {


                config.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
                    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                    .Enrich.WithMachineName()

                    .WriteTo.PostgreSQL(configuration["ConnectionStrings:SerilogConnection"], //"User ID=postgres;Password=1234;Host=localhost;Port=5432;Database=serilog;",
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

        if (serilogUi)
        {
            builder.Services.AddSerilogUi(options => options
                      .UseNpgSql(optionsDb => optionsDb.WithConnectionString(configuration["ConnectionStrings:SerilogConnection"])
                                                  .WithTable("logs")
                                                  .WithSinkType(PostgreSqlSinkType.SerilogSinksPostgreSQLAlternative)
                                                  )
                      .RegisterDisabledSortForProviderKey("CorrelationId")
                      );

        }
    }


    public static void UseTracesAndLogs(this WebApplication app, bool serilogUi)
    {
        app.UseCorrelationId();
        app.UseParentRequestId();
        app.UseRequestLogging();

        if (serilogUi)
        {
            //app.UseSerilogUi();//serilog-ui  
            app.UseSerilogUi(options =>
            {
                options.WithAuthenticationType(AuthenticationType.Basic);
                options.WithRoutePrefix("serilog-ui");

            });
        }
    }

    //Logs
    private static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
      => app.Use(async (ctx, next) =>
      {
          if (!ctx.Request.Path.Value.ToLower().Contains("serilog-ui"))
          {
              ctx.Request.EnableBuffering();

              var requestLog = new
              {
                  Path = ctx.Request.Path,
                  QueryString = ctx.Request.QueryString.ToString(),
                  Method = ctx.Request.Method,
                  Headers = ctx.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                  Body = await ReadRequestBody(ctx.Request),
                  Timestamp = DateTime.UtcNow
              };

              Log.Information("Request: {@RequestLog}", requestLog);

              ctx.Request.Body.Position = 0;
          }

          await next();
      });

    private static async Task<string> ReadRequestBody(HttpRequest request)
    {
        if (request.Body != null)
        {
            if (request.Body.Length > 0)
            {
                request.Body.Position = 0;
                using (StreamReader reader = new StreamReader(request.Body))
                {
                    return await reader.ReadToEndAsync();
                }

            }
        }

        return await Task.FromResult<string>(null!);
    }

    //CorrelationId
    private static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
     => app.Use(async (ctx, next) =>
     {
         if (!ctx.Request.Headers.TryGetValue(CorrelationIdKey, out var correlationId))
         {
             correlationId = Guid.NewGuid().ToString("N");
         }

         ctx.Items[CorrelationIdKey] = correlationId.ToString();

         LogContext.PushProperty("CorrelationId", correlationId.ToString());
         Activity.Current?.SetTag("CorrelationId", correlationId.ToString());

         await next();
     });

    public static void AddCorrelationId(this HttpRequestHeaders headers, string correlationId)
        => headers.TryAddWithoutValidation(CorrelationIdKey, correlationId);
    public static string? GetCorrelationId(this HttpContext context)
        => context.Items.TryGetValue(CorrelationIdKey, out var correlationId) ? correlationId as string : null;

    //ParentRequestId
    private static IApplicationBuilder UseParentRequestId(this IApplicationBuilder app)
    => app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Headers.TryGetValue(CorrelationIdKey, out var parentRequestIdKey))
        {
            ctx.Items[CorrelationIdKey] = parentRequestIdKey.ToString();
        }

        await next();
    });

    public static void AddParentRequestId(this HttpRequestHeaders headers, string parentRequestId)
        => headers.TryAddWithoutValidation(ParentRequestIdKey, parentRequestId);
    public static string? GetParentRequestId(this HttpContext context)
        => context.Items.TryGetValue(ParentRequestIdKey, out var parentRequestId) ? parentRequestId as string : null;

}