import { NodeSDK } from "@opentelemetry/sdk-node";
import { Resource } from "@opentelemetry/resources";
import { SimpleSpanProcessor } from "@opentelemetry/sdk-trace-node";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-proto";
import { TelemetryInitStrategy } from "./telemetry-init-strategy.interface";

/**
 * Local development telemetry initialization strategy via OpenTelemetry
 */
export class OtelTelemetryStrategy implements TelemetryInitStrategy {
  /**
   * Initialize telemetry for local development
   * @param resource OpenTelemetry resource
   * @returns The initialized SDK
   */
  initialize(resource: Resource): NodeSDK {
    const traceExporter = new OTLPTraceExporter();

    // Create the SDK
    const sdk = new NodeSDK({
      resource,
      traceExporter,
      spanProcessor: new SimpleSpanProcessor(traceExporter),
    });

    return sdk;
  }

  name: string = "OpenTelemetry";
}
