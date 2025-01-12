declare namespace NodeJS {
  interface ProcessEnv {
    // Environment
    NODE_ENV: "development" | "production" | "test";

    // Session
    SESSION_SECRET: string;
    
    // Data Collection
    NEXT_FORMS_COOKIE_NAME: string;
    NEXT_FORMS_COOKIE_DURATION_DAYS: string;

    // Slack
    SLACK_CLIENT_ID: string;
    SLACK_CLIENT_SECRET: string;
    SLACK_REDIRECT_URI: string;

    // Storage
    AZURE_STORAGE_ACCOUNT_NAME?: string;
    AZURE_STORAGE_ACCOUNT_KEY?: string;
    USER_FILES_STORAGE_CONTAINER_NAME?: string;
    CONTENT_STORAGE_CONTAINER_NAME?: string;
    
    // Image Resize
    RESIZE_IMAGES: string;
    RESIZE_IMAGES_WIDTH: string;

    // Public
    NEXT_PUBLIC_SLK: string;
    NEXT_PUBLIC_NAME: string;

    // Telemetry
    OTEL_LOG_LEVEL: boolean;
    APPLICATIONINSIGHTS_CONNECTION_STRING: string;
  }
}
