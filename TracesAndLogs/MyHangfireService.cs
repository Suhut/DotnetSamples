using System.Diagnostics;
using System.Net.Http;
using TracesAndLogs.Shared.Observability;

namespace TracesAndLogs;
public class MyHangfireService
{
    private readonly IHttpClientFactory _httpClientFactory; 

    public MyHangfireService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory; 
    }

    public async Task MyBackgroundMethod(string correlationId)
    {
        var client = _httpClientFactory.CreateClient("api3"); 
        var requestId = Activity.Current?.Id;
        if (requestId != null)
        {
            client.DefaultRequestHeaders.AddCorrelationId(correlationId);
            client.DefaultRequestHeaders.AddParentRequestId(requestId);
        }

        var response = await client.GetAsync("WeatherForecast");
          
    }
}