declare namespace NodeJS {
  interface ProcessEnv {
    NODE_ENV: "development" | "production" | "test";
    NEXT_FORMS_COOKIE_NAME: string;
    NEXT_FORMS_COOKIE_DURATION_DAYS: string;
    SESSION_SECRET: string;
    SLACK_CLIENT_ID: string;
    SLACK_CLIENT_SECRET: string;
    SLACK_REDIRECT_URI: string;
    AZURE_STORAGE_CONNECTION_STRING: string;
    USER_FILES_STORAGE_CONTAINER_NAME: string;
    CONTENT_STORAGE_CONTAINER_NAME: string;
    RESIZE_IMAGES: string;
    RESIZE_IMAGES_WIDTH: string;
    NEXT_PUBLIC_SLK: string;
  }
}
