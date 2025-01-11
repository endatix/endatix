import { Resource } from "@opentelemetry/resources";
import { NodeSDK } from "@opentelemetry/sdk-node";
import { ATTR_SERVICE_NAME } from "@opentelemetry/semantic-conventions";
import { getNodeAutoInstrumentations } from "@opentelemetry/auto-instrumentations-node";
import type { SpanExporter } from "@opentelemetry/sdk-trace-base";
import {
  AzureMonitorLogExporter,
  AzureMonitorTraceExporter,
} from "@azure/monitor-opentelemetry-exporter";
import { logs } from "@opentelemetry/api-logs";
import {
  LoggerProvider,
  BatchLogRecordProcessor,
} from "@opentelemetry/sdk-logs";
import { BatchSpanProcessor } from "@opentelemetry/sdk-trace-node";

const appInsightsConnectionString =
  process.env.APPLICATIONINSIGHTS_CONNECTION_STRING;
if (appInsightsConnectionString) {
  const traceExporter: SpanExporter | undefined = new AzureMonitorTraceExporter({
    connectionString: appInsightsConnectionString,
  });
  const logExporter = new AzureMonitorLogExporter({
    connectionString: appInsightsConnectionString,
  });

  const logRecordProcessor = new BatchLogRecordProcessor(logExporter);
  const loggerProvider = new LoggerProvider();
  loggerProvider.addLogRecordProcessor(logRecordProcessor);
  logs.setGlobalLoggerProvider(loggerProvider);

  const sdk = new NodeSDK({
    resource: new Resource({
      [ATTR_SERVICE_NAME]: "endatix-hub",
    }),
    traceExporter: traceExporter,
    spanProcessor: new BatchSpanProcessor(traceExporter),
    logRecordProcessor: logRecordProcessor,
    instrumentations: [getNodeAutoInstrumentations()],
  });

  sdk.start();
}
