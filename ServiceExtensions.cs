using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;

namespace ChrisKaczor.Common.OpenTelemetry;

public static class ServiceExtensions
{
    [PublicAPI]
    public static void AddCommonOpenTelemetry(this IServiceCollection serviceCollection, string serviceName, string telemetryEndpoint, string activitySourceName)
    {
        serviceCollection.AddCommonOpenTelemetry(serviceName, telemetryEndpoint, new[] { activitySourceName });
    }

    [PublicAPI]
    public static void AddCommonOpenTelemetry(this IServiceCollection serviceCollection, string serviceName, string telemetryEndpoint, IEnumerable<string> activitySourceNames)
    {
        // ---

        var openTelemetry = serviceCollection.AddOpenTelemetry();

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        openTelemetry.ConfigureResource(resource => resource.AddService(serviceName));

        openTelemetry.WithMetrics(meterProviderBuilder =>
        {
            meterProviderBuilder.AddAspNetCoreInstrumentation();
            meterProviderBuilder.AddHttpClientInstrumentation();
            meterProviderBuilder.AddProcessInstrumentation();
            meterProviderBuilder.AddMeter("Microsoft.AspNetCore.Hosting");
            meterProviderBuilder.AddMeter("Microsoft.AspNetCore.Server.Kestrel");
        });

        openTelemetry.WithTracing(tracerProviderBuilder =>
        {
            tracerProviderBuilder.AddAspNetCoreInstrumentation(instrumentationOptions => { instrumentationOptions.RecordException = true; });
            tracerProviderBuilder.AddHttpClientInstrumentation(instrumentationOptions => { instrumentationOptions.RecordException = true; });

            tracerProviderBuilder.AddSqlClientInstrumentation(o =>
            {
                o.RecordException = true;
                o.SetDbStatementForText = true;
            });

            foreach (var activitySourceName in activitySourceNames)
            {
                tracerProviderBuilder.AddSource(activitySourceName);
            }

            tracerProviderBuilder.SetErrorStatusOnException();

            tracerProviderBuilder.AddOtlpExporter(exporterOptions =>
            {
                exporterOptions.Endpoint = new Uri(telemetryEndpoint);
                exporterOptions.Protocol = OtlpExportProtocol.Grpc;
            });
        });

        serviceCollection.AddLogging(loggingBuilder =>
        {
            loggingBuilder.SetMinimumLevel(LogLevel.Information);

            loggingBuilder.AddOpenTelemetry(options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                    options.ParseStateValues = true;

                    options.AddOtlpExporter(exporterOptions =>
                    {
                        exporterOptions.Endpoint = new Uri(telemetryEndpoint);
                        exporterOptions.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
            );
        });
    }
}