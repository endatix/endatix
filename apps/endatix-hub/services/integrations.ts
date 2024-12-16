import { SlackAuthResponse } from "@/types";
import { HeaderBuilder } from "./header-builder";

const API_BASE_URL = `${process.env.ENDATIX_BASE_URL}/api`;

export const getSlackBearerToken = async (
  code: string
): Promise<SlackAuthResponse> => {
  const requestOptions: RequestInit = {};
  
  const client_id = "8095858636565.8147306484534";
  const client_secret = "{slack-app-secret-goes-here}";
  const redirect_uri = "temp-url"

  const response = await fetch(
    `https://slack.com/api/oauth.v2.access?code=${code}&client_id=${client_id}&client_secret=${client_secret}&redirect_uri=${redirect_uri}`,
    requestOptions
  );

  if (!response.ok) {
    throw new Error("error");
  }

  return response.json();
};

export const sendSlackBearerToken = async (
    token: string
  ): Promise<string> => {
    const requestOptions: RequestInit = {};

    const headers = new HeaderBuilder()
        .acceptJson()
        .provideJson()
        .build();
  
    var endpointUrl = `${API_BASE_URL}/slacktoken`;

    const response = await fetch(endpointUrl, {
        method: "POST",
        headers: headers,
        body: "{ \"token\": \"" + token + "\"}"
      });
  
    if (!response.ok) {
      throw new Error("error");
    }
  
    return "ок";
  };