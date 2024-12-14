declare namespace NodeJS {
  interface ProcessEnv {
    NEXT_FORMS_COOKIE_NAME: string;
    NEXT_FORMS_COOKIE_DURATION_DAYS: string;
    SESSION_SECRET: string;
    NODE_ENV: 'development' | 'production' | 'test';
  }
} 