export interface SessionData {
  username: string;
  accessToken: string;
  refreshToken: string;
  isLoggedIn: boolean;
}

export interface CookieOptions {
  name: string;
  encryptionKey: string;
  secure: boolean;
  httpOnly: boolean;
}

export interface AuthenticationResponse {
  email: string;
  accessToken: string;
  refreshToken: string;
}

export interface AuthenticationRequest {
  email: string;
  password: string;
}