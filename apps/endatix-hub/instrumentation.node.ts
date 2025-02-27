import { TelemetryInitializer } from './features/telemetry/infrastructure/telemetry-initializer';

/**
 * Initialize OpenTelemetry instrumentation for Node.js
 * 
 * This file serves as the entry point for OpenTelemetry instrumentation.
 * It determines whether to use Azure Application Insights or a local OTLP exporter
 * based on environment configuration.
 */

// Create and start the telemetry initializer
const telemetryInitializer = new TelemetryInitializer();
telemetryInitializer.initialize(); 