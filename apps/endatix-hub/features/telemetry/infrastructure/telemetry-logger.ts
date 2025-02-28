import { logs, SeverityNumber } from "@opentelemetry/api-logs";

/**
 * Severity levels for logging
 */
export enum LogSeverity {
  /**
   * Detailed debug information
   */
  Debug = "DEBUG",

  /**
   * Interesting events
   */
  Info = "INFO",

  /**
   * Unexpected warnings
   */
  Warning = "WARNING",

  /**
   * Error events that might still allow the application to continue running
   */
  Error = "ERROR",

  /**
   * Critical conditions
   */
  Critical = "CRITICAL",
}

/**
 * Maps LogSeverity enum to OpenTelemetry SeverityNumber
 */
const severityMap: Record<LogSeverity, SeverityNumber> = {
  [LogSeverity.Debug]: SeverityNumber.DEBUG,
  [LogSeverity.Info]: SeverityNumber.INFO,
  [LogSeverity.Warning]: SeverityNumber.WARN,
  [LogSeverity.Error]: SeverityNumber.ERROR,
  [LogSeverity.Critical]: SeverityNumber.FATAL,
};

/**
 * Log record attributes
 */
export interface LogAttributes {
  [key: string]: string | number | boolean | undefined;
}

/**
 * Provides utilities for logging with OpenTelemetry
 */
export class TelemetryLogger {
  private static readonly DEFAULT_LOGGER_NAME = "default";

  /**
   * Gets a logger with the given name
   * @param name Logger name
   */
  static getLogger(name: string = this.DEFAULT_LOGGER_NAME) {
    return logs.getLogger(name);
  }

  /**
   * Logs a message with the specified severity
   * @param message Message to log
   * @param severity Severity level
   * @param attributes Additional attributes to include
   * @param loggerName Name of the logger
   */
  static log(
    message: string,
    severity: LogSeverity = LogSeverity.Info,
    attributes: LogAttributes = {},
    loggerName?: string,
  ): void {
    const logger = this.getLogger(loggerName);

    // Add standard attributes
    const enhancedAttributes = {
      "log.type": "LogRecord",
      ...attributes,
    };

    // Emit the log record
    logger.emit({
      severityNumber: severityMap[severity],
      severityText: severity,
      body: message,
      attributes: enhancedAttributes,
    });
  }

  /**
   * Logs a debug message
   * @param message Message to log
   * @param attributes Additional attributes
   * @param loggerName Logger name
   */
  static debug(
    message: string,
    attributes?: LogAttributes,
    loggerName?: string,
  ): void {
    this.log(message, LogSeverity.Debug, attributes, loggerName);
  }

  /**
   * Logs an info message
   * @param message Message to log
   * @param attributes Additional attributes
   * @param loggerName Logger name
   */
  static info(
    message: string,
    attributes?: LogAttributes,
    loggerName?: string,
  ): void {
    this.log(message, LogSeverity.Info, attributes, loggerName);
  }

  /**
   * Logs a warning message
   * @param message Message to log
   * @param attributes Additional attributes
   * @param loggerName Logger name
   */
  static warn(
    message: string,
    attributes?: LogAttributes,
    loggerName?: string,
  ): void {
    this.log(message, LogSeverity.Warning, attributes, loggerName);
  }

  /**
   * Logs an error message
   * @param message Message to log
   * @param error Optional error object
   * @param attributes Additional attributes
   * @param loggerName Logger name
   */
  static error(
    message: string,
    error?: Error,
    attributes?: LogAttributes,
    loggerName?: string,
  ): void {
    const enhancedAttributes: LogAttributes = {
      ...attributes,
    };

    if (error) {
      enhancedAttributes["error.message"] = error.message;
      enhancedAttributes["error.stack"] = error.stack;
      enhancedAttributes["error.name"] = error.name;
    }

    this.log(message, LogSeverity.Error, enhancedAttributes, loggerName);
  }

  /**
   * Logs a critical message
   * @param message Message to log
   * @param error Optional error object
   * @param attributes Additional attributes
   * @param loggerName Logger name
   */
  static critical(
    message: string,
    error?: Error,
    attributes?: LogAttributes,
    loggerName?: string,
  ): void {
    const enhancedAttributes: LogAttributes = {
      ...attributes,
    };

    if (error) {
      enhancedAttributes["error.message"] = error.message;
      enhancedAttributes["error.stack"] = error.stack;
      enhancedAttributes["error.name"] = error.name;
    }

    this.log(message, LogSeverity.Critical, enhancedAttributes, loggerName);
  }
}
