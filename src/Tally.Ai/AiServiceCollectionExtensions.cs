using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Tally.Domain.Receipts;

namespace Tally.Ai;

public static class AiServiceCollectionExtensions
{
    // One switch in config picks the provider; the rest of the app never knows which one.
    public static IServiceCollection AddTallyAi(this IServiceCollection services, IConfiguration config)
    {
        var ai = config.GetSection("Ai");
        var apiKey = ai["ApiKey"] ?? throw new InvalidOperationException("Ai:ApiKey is missing.");
        var endpoint = ai["Endpoint"] ?? throw new InvalidOperationException("Ai:Endpoint is missing.");

        if (ai["Provider"] == "AzureOpenAI")
        {
            services.AddAzureOpenAIChatCompletion(
                deploymentName: ai["Deployment"] ?? "gpt-4o-mini",
                endpoint: endpoint,
                apiKey: apiKey);
        }
        else
        {
#pragma warning disable SKEXP0010   // custom endpoint for OpenAI-compatible providers is marked experimental
            services.AddOpenAIChatCompletion(
                modelId: ai["Model"] ?? "gpt-4o-mini",
                endpoint: new Uri(endpoint),
                apiKey: apiKey);
#pragma warning restore SKEXP0010
        }

        services.AddScoped<IReceiptReader, ReceiptReader>();
        return services;
    }
}