using Serilog;

namespace SerilogUI;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if(!context.Request.Path.Value.Contains("serilog-ui"))
        {
            context.Request.EnableBuffering();

            var requestLog = new
            {
                Path = context.Request.Path,
                QueryString = context.Request.QueryString.ToString(),
                Method = context.Request.Method,
                Headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                Body = await ReadRequestBody(context.Request),
                Timestamp = DateTime.UtcNow
            };

            Log.Information("Request: {@RequestLog}", requestLog);

            context.Request.Body.Position = 0;
        }
       


        await _next(context);
    }

    private async Task<string> ReadRequestBody(HttpRequest request)
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
}
