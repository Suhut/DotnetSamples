using NpgsqlTypes;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.PostgreSQL.ColumnWriters;
using Serilog.Ui.Core.OptionsBuilder;
using Serilog.Ui.MsSqlServerProvider.Extensions;
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
    { "PropertyTest", new PropertiesColumnWriter(NpgsqlDbType.Text) }, 
    { "MachineName", new SinglePropertyColumnWriter("MachineName", format: "l") },
    { "SpanId", new SpanIdColumnWriterBase() }
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

            .WriteTo.MSSqlServer("Server=SUHUT-TUF;Database=serilog;TrustServerCertificate=True;Trusted_Connection=True;MultipleActiveResultSets=true;Application Name=serilog;",
                                    "logs", 
                                    autoCreateSqlTable: true,
                                    batchPostingLimit: 1
                                    )
            ;

        if (context.HostingEnvironment.IsDevelopment())
            config.WriteTo.Console(new RenderedCompactJsonFormatter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



//version : 3.*
builder.Services.AddSerilogUi(options => options
    .UseNpgSql(optionsDb => optionsDb.WithConnectionString("User ID=postgres;Password=1234;Host=localhost;Port=5432;Database=serilog;")
                                .WithTable("logs") 
                                .WithSinkType(PostgreSqlSinkType.SerilogSinksPostgreSQLAlternative)  
                                )
    .UseSqlServer(optionsDb => optionsDb.WithConnectionString("Server=SUHUT-TUF;Database=serilog;TrustServerCertificate=True;Trusted_Connection=True;MultipleActiveResultSets=true;Application Name=serilog;")
                                    .WithTable("logs")
                                    )
    ); 


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseAuthentication();
//app.UseAuthorization();

app.UseSerilogUi();//serilog-ui  


app.MapGet("/cmdSuccess", async () =>
{
    var random = new Random();
    int dice = random.Next(0, 5);
    await Task.Delay(dice * 1000);

    Log.Information($"DELAY : {dice} ## cmdSuccess");

    return Results.Ok();
}).WithOpenApi();

app.MapGet("/cmdFail", async () =>
{
    var random = new Random();
    int dice = random.Next(0, 5);
    await Task.Delay(dice * 1000);

    Log.Error($"DELAY : {dice} ## cmdFail");

    return Results.BadRequest();
}).WithOpenApi();

app.MapGet("/", async () =>
{
    return Results.Ok();
}).WithOpenApi();

app.Run();

public class TranceIdColumnWriterBase : ColumnWriterBase
{
    public TranceIdColumnWriterBase(NpgsqlDbType dbType = NpgsqlDbType.Text) : base(dbType)
    {
        this.DbType = NpgsqlDbType.Text;
    }

    public override object GetValue(LogEvent logEvent, IFormatProvider formatProvider = null)
    {
        return logEvent.TraceId.ToString();
    }
}

public class SpanIdColumnWriterBase : ColumnWriterBase
{
    public SpanIdColumnWriterBase(NpgsqlDbType dbType = NpgsqlDbType.Text) : base(dbType)
    {
        this.DbType = NpgsqlDbType.Text;
    }

    public override object GetValue(LogEvent logEvent, IFormatProvider formatProvider = null)
    {
        return logEvent.SpanId.ToString();
    }
}