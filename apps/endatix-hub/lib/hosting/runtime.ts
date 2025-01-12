/**
 * Checks if the current runtime environment is Node.js
 * 
 * @returns {boolean} True if running in Node.js environment, false if running in other runtimes (e.g. Edge)
 * 
 * This is used to conditionally execute code that should only run in Node.js,
 * such as file system operations or Node-specific APIs.
 */
export const isNodeRuntime = () => {
    // NEXT_RUNTIME is undefined in regular Node.js environment
    // or explicitly set to 'nodejs' in Next.js Node runtime
    if (process.env.NEXT_RUNTIME === 'nodejs') {
        return true
    } else {
        return false
    }
}

/**
 * Checks if the current runtime environment is Edge
 * 
 * @returns {boolean} True if running in Edge runtime environment, false otherwise
 * 
 * This is used to conditionally execute code that should only run in Edge,
 * such as Edge-specific APIs or optimizations.
 */
export function isEdgeRuntime(): boolean {
    if (typeof process !== 'undefined' && process.env.NEXT_RUNTIME === 'edge') {
        return true;
    }

    return false;
}