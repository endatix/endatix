import { NodeSDK } from "@opentelemetry/sdk-node";
import { Resource } from "@opentelemetry/resources";
import {
  AzureMonitorLogExporter,
  AzureMonitorTraceExporter,
} from "@azure/monitor-opentelemetry-exporter";
import { BatchSpanProcessor } from "@opentelemetry/sdk-trace-node";
import { logs } from "@opentelemetry/api-logs";
import {
  LoggerProvider,
  BatchLogRecordProcessor,
} from "@opentelemetry/sdk-logs";
import { TelemetryConfig } from "../telemetry-config";
import { TelemetryInitStrategy } from "./telemetry-init-strategy.interface";

/**
 * Azure Application Insights telemetry initialization strategy
 */
export class AzureTelemetryStrategy implements TelemetryInitStrategy {
  /**
   * Initialize telemetry for Azure environment
   * @param resource OpenTelemetry resource
   * @returns The initialized SDK
   */
  initialize(resource: Resource): NodeSDK {
    const connectionString = TelemetryConfig.getConnectionString();

    // Create Azure Monitor trace exporter
    const traceExporter = new AzureMonitorTraceExporter({
      connectionString,
    });

    // Create log exporter and provider
    const logExporter = new AzureMonitorLogExporter({
      connectionString,
    });

    const loggerProvider = new LoggerProvider();
    loggerProvider.addLogRecordProcessor(
      new BatchLogRecordProcessor(logExporter),
    );

    // Register logger provider as global
    logs.setGlobalLoggerProvider(loggerProvider);

    // Create the SDK
    const sdk = new NodeSDK({
      resource,
      traceExporter,
      spanProcessor: new BatchSpanProcessor(traceExporter),
    });

    return sdk;
  }

  name: string = "Azure AppInsights";
}
