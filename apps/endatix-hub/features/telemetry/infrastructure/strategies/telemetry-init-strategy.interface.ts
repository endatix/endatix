import { NodeSDK } from "@opentelemetry/sdk-node";
import { Resource } from "@opentelemetry/resources";

/**
 * Strategy interface for telemetry SDK initialization
 */
export interface TelemetryInitStrategy {
  /**
   * Initialize and start the telemetry SDK
   * @param resource The OpenTelemetry resource
   * @returns The initialized SDK
   */
  initialize(resource: Resource): NodeSDK;

  /**
   * Get the name of the telemetry strategy
   * @returns The name of the telemetry strategy
   */
  name: string;
}
