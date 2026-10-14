using MeteringConsumption.MarketMessaging.Domain;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events;
using MeteringConsumption.MarketMessaging.Application.Features.Audit;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Classifiers;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Classifiers.RoiEsb;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies.ESB;
using MeteringConsumption.MarketMessaging.Application.Features.Receive;
using Microsoft.Extensions.DependencyInjection;

namespace MeteringConsumption.MarketMessaging.Application.Extensions;

public static class ApplicationRegistration
{
    public static IServiceCollection AddBaseApplication(this IServiceCollection services)
    {
        return services;
    }

    public static IServiceCollection AddReceiveApplication(this IServiceCollection services)
    {
        services.AddScoped<IEventHandler<BlobCreated>, ReceiveBlobCreatedEventHandler>();

        return services;
    }

    public static IServiceCollection AddInterpretApplication(this IServiceCollection services)
    {
        services.AddSingleton(new MessageClassifiers
        {
            [MeteringConsumptionTopology.Markets.RoiEsb] = new ESBXmlHeaderClassifier(),
        });

        services.AddScoped<IMessageStrategy, MM101Strategy>();
        services.AddScoped<IMessageStrategy, MM105Strategy>();
        services.AddScoped<IMessageStrategy, MM300Strategy>();
        services.AddScoped<IMessageStrategy, MM300SStrategy>();
        services.AddScoped<IMessageStrategy, MM301Strategy>();
        services.AddScoped<IMessageStrategy, MM306Strategy>();
        services.AddScoped<IMessageStrategy, MM307Strategy>();
        services.AddScoped<IMessageStrategy, MM310Strategy>();
        services.AddScoped<IMessageStrategy, MM320Strategy>();
        services.AddScoped<IMessageStrategy, MM331Strategy>();
        services.AddScoped<IMessageStrategy, MM332Strategy>();
        services.AddScoped<IMessageStrategy, MM343Strategy>();
        services.AddScoped<MessageStrategy>();
        services.AddScoped<InterpretMarketMessageReceivedEventHandler>();

        return services;
    }

    public static IServiceCollection AddAuditApplication(this IServiceCollection services)
    {
        services.AddScoped<AuditMarketMessageReceivedEventHandler>();
        services.AddScoped<ReceiptOutcomeRecorder>();
        services.AddScoped<AuditMarketMessageProcessedEventHandler>();
        services.AddScoped<AuditMarketMessageFailedEventHandler>();

        return services;
    }
}
