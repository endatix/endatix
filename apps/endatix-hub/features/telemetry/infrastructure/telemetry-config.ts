import { ATTR_SERVICE_NAME } from "@opentelemetry/semantic-conventions";

/**
 * Telemetry configuration constants
 */
export const TelemetryConfig = {
  /**
   * Application Insights connection string environment variable name
   */
  APP_INSIGHTS_CONNECTION_STRING_ENV: "APPLICATIONINSIGHTS_CONNECTION_STRING",

  /**
   * Service name for telemetry
   */
  SERVICE_NAME: "endatix-hub",

  /**
   * Resource attribute key for service name
   */
  ATTR_SERVICE_NAME,

  /**
   * Determine if Azure Application Insights is configured
   */
  isAzureConfigured(): boolean {
    return !!process.env.APPLICATIONINSIGHTS_CONNECTION_STRING;
  },

  /**
   * Get the Application Insights connection string
   */
  getConnectionString(): string {
    if (!this.isAzureConfigured()) {
      throw new Error(
        "Application Insights connection string is not configured",
      );
    }
    return process.env.APPLICATIONINSIGHTS_CONNECTION_STRING!;
  },
};
