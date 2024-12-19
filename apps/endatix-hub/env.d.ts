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
    AZURE_STORAGE_CONTAINER_NAME: string;
    RESIZE_IMAGES: boolean;
    RESIZE_IMAGES_WIDTH: string;
    NEXT_PUBLIC_MAX_IMAGE_SIZE: number;
    NEXT_PUBLIC_SLK: string;
  }
}
