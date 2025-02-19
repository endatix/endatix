import { JWTPayload } from "jose";

export interface EndatixJwtPayload extends JWTPayload {
  permission?: string[];
  role?: string[];
}

export interface HubJwtPayload extends JWTPayload {
  accessToken: string;
  refreshToken: string;
  sub: string;
}
