import { z } from "zod";

export type AuthenticationResponse = {
  email: string;
  accessToken: string;
  refreshToken: string;
};

export type AuthenticationRequest = {
  email: string;
  password: string;
};

export const AuthenticationRequestSchema = z.object({
  email: z.string().email({ message: "Please enter a valid email." }).trim(),
  password: z
    .string()
    .min(8, { message: "Be at least 8 characters long" })
    .trim(),
});