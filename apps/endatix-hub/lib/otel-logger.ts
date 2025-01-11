import { logs } from "@opentelemetry/api-logs";
import type { LogRecord as APILogRecord } from "@opentelemetry/api-logs";

/**
 * Logs an information level message using OpenTelemetry logging.
 * 
 * @param text - The message text to log
 * @param origin - The origin/source of the log message (e.g. component name, feature name)
 * 
 * @example
 * ```ts
 * logInformation("User logged in successfully", "LoginForm")
 * ```
 */
export function logInformation(text: string, origin: string) {
    const logRecord: APILogRecord = {
        attributes: {
            "severityLevel": "Information", 
            "origin": origin,
        },
        severityNumber: 3,
        body: text,
    };
    logs.getLogger("endatix-hub").emit(logRecord);
}