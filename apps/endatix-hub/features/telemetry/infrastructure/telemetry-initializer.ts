import { NodeSDK } from '@opentelemetry/sdk-node';
import { Resource } from '@opentelemetry/resources';
import {
  AzureMonitorLogExporter,
  AzureMonitorTraceExporter,
} from '@azure/monitor-opentelemetry-exporter';
import { BatchSpanProcessor, SimpleSpanProcessor } from '@opentelemetry/sdk-trace-node';
import { logs } from '@opentelemetry/api-logs';
import {
  LoggerProvider,
  BatchLogRecordProcessor,
} from '@opentelemetry/sdk-logs';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-proto';
import { TelemetryConfig } from './telemetry-config';

/**
 * Strategy interface for telemetry SDK initialization
 */
interface TelemetryInitStrategy {
  /**
   * Initialize and start the telemetry SDK
   * @param resource The OpenTelemetry resource
   * @returns The initialized SDK
   */
  initialize(resource: Resource): NodeSDK;
}

/**
 * Azure Application Insights telemetry initialization strategy
 */
class AzureTelemetryStrategy implements TelemetryInitStrategy {
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
      new BatchLogRecordProcessor(logExporter)
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
}

/**
 * Local development telemetry initialization strategy
 */
class LocalTelemetryStrategy implements TelemetryInitStrategy {
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
}

/**
 * Telemetry initializer responsible for setting up and starting telemetry
 */
export class TelemetryInitializer {
  private sdk: NodeSDK | null = null;
  private strategy: TelemetryInitStrategy;
  private resource: Resource;
  
  /**
   * Create a telemetry initializer with the appropriate strategy based on environment
   */
  constructor() {
    this.resource = new Resource({
      [TelemetryConfig.ATTR_SERVICE_NAME]: TelemetryConfig.SERVICE_NAME,
    });
    
    this.strategy = TelemetryConfig.isAzureConfigured()
      ? new AzureTelemetryStrategy()
      : new LocalTelemetryStrategy();
  }
  
  /**
   * Initialize and start the telemetry SDK
   */
  initialize(): void {
    try {
      this.sdk = this.strategy.initialize(this.resource);
      this.sdk.start();
      
      // Register shutdown handler
      this.registerShutdownHandler();
      
      const mode = TelemetryConfig.isAzureConfigured() ? 'Azure' : 'local';
      console.log(`Telemetry SDK started in ${mode} mode`);
    } catch (error) {
      console.error('Failed to initialize telemetry:', error);
    }
  }
  
  /**
   * Register handlers for graceful shutdown
   */
  private registerShutdownHandler(): void {
    if (!this.sdk) return;
    
    const shutdownHandler = () => {
      if (this.sdk) {
        this.sdk
          .shutdown()
          .then(
            () => console.log('Telemetry SDK shut down successfully'),
            (err) => console.error('Error shutting down Telemetry SDK', err),
          )
          .finally(() => process.exit(0));
      }
    };
    
    // Register for various termination signals
    process.on('SIGTERM', shutdownHandler);
    process.on('SIGINT', shutdownHandler);
    
    // For local development, also handle process exit
    if (!TelemetryConfig.isAzureConfigured()) {
      process.on('beforeExit', shutdownHandler);
    }
  }
} 