import { trace, Span } from "@opentelemetry/api";

/**
 * Provides utilities for tracing operations with OpenTelemetry
 */
export class TelemetryTracer {
  /**
   * Creates a tracer with the given name
   * @param tracerName Name of the tracer
   */
  static getTracer(tracerName: string) {
    return trace.getTracer(tracerName);
  }

  /**
   * Wraps a function execution in a span
   * @param tracerName Name of the tracer
   * @param spanName Name of the span
   * @param fn Function to execute
   * @returns Result of the function
   */
  static async traceAsync<T>(
    tracerName: string,
    spanName: string,
    fn: (span: Span) => Promise<T>,
  ): Promise<T> {
    return this.getTracer(tracerName).startActiveSpan(
      spanName,
      async (span) => {
        try {
          return await fn(span);
        } catch (error) {
          span.recordException(error as Error);
          span.setStatus({ code: 2 }); // Error
          throw error;
        } finally {
          span.end();
        }
      },
    );
  }

  /**
   * Wraps a synchronous function execution in a span
   * @param tracerName Name of the tracer
   * @param spanName Name of the span
   * @param fn Function to execute
   * @returns Result of the function
   */
  static trace<T>(
    tracerName: string,
    spanName: string,
    fn: (span: Span) => T,
  ): T {
    return this.getTracer(tracerName).startActiveSpan(spanName, (span) => {
      try {
        return fn(span);
      } catch (error) {
        span.recordException(error as Error);
        span.setStatus({ code: 2 }); // Error
        throw error;
      } finally {
        span.end();
      }
    });
  }
}
