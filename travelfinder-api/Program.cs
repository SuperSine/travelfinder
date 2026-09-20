using System.Net;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using TravelfinderAPI;
using TravelfinderAPI.Agents;
using TravelfinderAPI.Gis;
using TravelfinderAPI.Gis.Providers;
using TravelfinderAPI.GmpGis;
using TravelfinderAPI.Host;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddMemoryCache();

builder.Services.AddScoped<GmpGisApiClient>(services =>
{
    var configuration = services.GetService<IConfiguration>();

    var apiKey = configuration != null ? configuration["GMPGIS_API_KEY"] : "";
    var enableProxy = configuration != null ? configuration["ENABLE_PROXY"] : "";
    var isProxy = Convert.ToBoolean(enableProxy);

    return new GmpGisApiClient(apiKey, isProxy);
});

builder.Services.AddScoped<ArcGisApiClient>(services =>
{
    var configuration = services.GetService<IConfiguration>();

    var apiKey = configuration != null ? configuration["ARCGIS_API_KEY"] : "";
    var enableProxy = configuration != null ? configuration["ENABLE_PROXY"] : "";
    var isProxy = Convert.ToBoolean(enableProxy);

    var featureLayers = configuration.GetSection("FEATURE_LAYER").Get<string[]>();
    var pointLayerUrl = featureLayers[1];

    return new ArcGisApiClient(apiKey, isProxy, pointLayerUrl);
});

builder.Services.AddHttpClient("GooglePlaces", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri("https://places.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(config.GetValue("Planning:ProviderTimeoutSeconds", 5));
    var key = config["GMPGIS_API_KEY"] ?? "";
    if (!string.IsNullOrEmpty(key))
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Goog-Api-Key", key);
    }
}).ConfigurePrimaryHttpMessageHandler(sp => CreateHandler(sp.GetRequiredService<IConfiguration>()));

builder.Services.AddHttpClient("ArcGisFeature", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.Timeout = TimeSpan.FromSeconds(config.GetValue("Planning:ProviderTimeoutSeconds", 5));
}).ConfigurePrimaryHttpMessageHandler(sp => CreateHandler(sp.GetRequiredService<IConfiguration>()));

builder.Services.AddSingleton<IPlaceProvider>(sp =>
    new GooglePlaceProvider(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("GooglePlaces"),
        sp.GetRequiredService<IMemoryCache>(),
        sp.GetRequiredService<IConfiguration>()["GMPGIS_API_KEY"]));

builder.Services.AddSingleton<IPlaceProvider>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var layers = config.GetSection("FEATURE_LAYER").Get<string[]>() ?? [];
    var layerUrl = layers.Length > 1 ? layers[1] : "";
    return new ArcGisPlaceProvider(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("ArcGisFeature"),
        sp.GetRequiredService<IMemoryCache>(),
        layerUrl);
});

builder.Services.AddSingleton<IPlaceService, PlaceService>();
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var azure = CreateAzureChatClient(config);
    var xai = CreateXaiChatClient(config);
    return new TravelfinderAPI.Agents.FailoverChatClient(azure, xai);
});
builder.Services.AddSingleton<IPlanner, PlannerAgent>();
builder.Services.AddSingleton<IRenderer, RendererAgent>();
builder.Services.AddSingleton<PlanOrchestrator>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(x => x.AddPolicy("AllowAll", builder =>
{
    builder.AllowAnyOrigin()
           .AllowAnyMethod()
           .AllowAnyHeader();
}));

var app = builder.Build();

app.UseCors(builder =>
{
    builder
       .WithOrigins("http://localhost:4200", "https://localhost:4200", "https://chat.x-rank.com", "http://localhost:8100", "http://localhost:45541")
       .SetIsOriginAllowedToAllowWildcardSubdomains()
       .AllowAnyHeader()
       .AllowCredentials()
       .WithMethods("GET", "PUT", "POST", "DELETE", "OPTIONS")
       .SetPreflightMaxAge(TimeSpan.FromSeconds(3600));
}
);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();


app.MapControllerRoute("default", "{controller=Home}/{action=index}/{id?}");


app.Run();

static HttpClientHandler CreateHandler(IConfiguration config)
{
    var enableProxy = Convert.ToBoolean(config["ENABLE_PROXY"]);
    if (enableProxy)
    {
        var proxy = new WebProxy
        {
            Address = new Uri("socks5://127.0.0.1:2085"),
            BypassProxyOnLocal = false,
            UseDefaultCredentials = false,
        };

        return new HttpClientHandler
        {
            Proxy = proxy,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
    }

    return new HttpClientHandler();
}

static IChatClient CreateAzureChatClient(IConfiguration config)
{
    var endpoint = config["Planning:AzureEndpoint"] ?? "https://travelfinder.openai.azure.com/";
    var deployment = config["Planning:AzureDeployment"] ?? "gpt-4-2";
    var key = config["AZUREAI_API_KEY"] ?? "";
    return new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(key))
        .GetChatClient(deployment)
        .AsIChatClient();
}

static IChatClient CreateXaiChatClient(IConfiguration config)
{
    var endpoint = config["Planning:XaiEndpoint"] ?? "https://api.x.ai/v1";
    var model = config["Planning:XaiModel"] ?? "grok-2-1212";
    var key = config["XAI_API_KEY"] ?? "";
    var openAi = new OpenAI.Chat.ChatClient(model, new System.ClientModel.ApiKeyCredential(key), new OpenAI.OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint)
    });
    return openAi.AsIChatClient();
}
