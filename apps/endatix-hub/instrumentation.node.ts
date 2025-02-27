import { Resource } from "@opentelemetry/resources";
import { NodeSDK } from "@opentelemetry/sdk-node";
import { ATTR_SERVICE_NAME } from "@opentelemetry/semantic-conventions";
import type { SpanExporter } from "@opentelemetry/sdk-trace-base";
import {
  AzureMonitorLogExporter,
  AzureMonitorTraceExporter,
} from "@azure/monitor-opentelemetry-exporter";
import { BatchSpanProcessor, SimpleSpanProcessor } from "@opentelemetry/sdk-trace-node";
import { logs } from "@opentelemetry/api-logs";
import {
  LoggerProvider,
  BatchLogRecordProcessor,
} from "@opentelemetry/sdk-logs";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-proto";

const appInsightsConnectionString =
  process.env.APPLICATIONINSIGHTS_CONNECTION_STRING;
if (appInsightsConnectionString) {
  const appInsightsTraceExporter: SpanExporter | undefined =
    new AzureMonitorTraceExporter({
      connectionString: appInsightsConnectionString,
    });

  const appInsightsLogExporter = new AzureMonitorLogExporter({
    connectionString: appInsightsConnectionString,
  });
  const logRecordProcessor = new BatchLogRecordProcessor(
    appInsightsLogExporter,
  );
  const loggerProvider = new LoggerProvider();
  loggerProvider.addLogRecordProcessor(logRecordProcessor);

  // Register logger Provider as global
  logs.setGlobalLoggerProvider(loggerProvider);

  const sdk = new NodeSDK({
    resource: new Resource({
      [ATTR_SERVICE_NAME]: "endatix-hub",
    }),
    traceExporter: appInsightsTraceExporter,
    spanProcessor: new BatchSpanProcessor(appInsightsTraceExporter),
  });

  sdk.start();
} else {
  const sdk = new NodeSDK({
    resource: new Resource({
      [ATTR_SERVICE_NAME]: "endatix-hub",
    }),
    spanProcessor: new SimpleSpanProcessor(new OTLPTraceExporter()),
  });

  process.on("SIGTERM", () =>
    sdk
      .shutdown()
      .then(
        () => console.log("OTEL SDK shut down successfully"),
        (err) => console.log("Error shutting down OTEL SDK", err),
      )
      .finally(() => process.exit(0)),
  );

  sdk.start();
}
