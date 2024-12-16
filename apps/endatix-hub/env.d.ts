declare namespace NodeJS {
  interface ProcessEnv {
    NEXT_FORMS_COOKIE_NAME: string;
    NEXT_FORMS_COOKIE_DURATION_DAYS: string;
    SESSION_SECRET: string;
    NODE_ENV: 'development' | 'production' | 'test';
    SLACK_CLIENT_ID: string;
    SLACK_CLIENT_SECRET: string;
    SLACK_REDIRECT_URI: string;
  }
} 