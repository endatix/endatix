import { SessionData } from "@/features/auth";

export class HeaderBuilder {
  private headers: Record<string, string> = {};

  withAuth(session?: SessionData): HeaderBuilder {
    if (session?.accessToken) {
      this.headers["Authorization"] = `Bearer ${session.accessToken}`;
    }
    return this;
  }

  acceptJson(): HeaderBuilder {
    this.headers["Accept"] = "application/json";
    return this;
  }

  provideJson(): HeaderBuilder {
    this.headers["Content-Type"] = "application/json";
    return this;
  }

  build(): Record<string, string> {
    return { ...this.headers };
  }
}
