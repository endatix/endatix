"use client";

import { useSearchParams } from "next/navigation"; 
import { getSlackBearerToken, sendSlackBearerToken } from "@/services/integrations";

export default async function Slack() {
  const searchParams = useSearchParams(); 
  const code = searchParams.get("code");
  var message = "";

  var responseJson = await getSlackBearerToken(code ?? "");

  if(responseJson.ok) {
    var result = await sendSlackBearerToken(responseJson.access_token);

    if(result) {
      message = "Successfully received token";
    }
    else {
      message = "Failed to received token";
    }
  }
  else {
    message = responseJson.error;
  }

  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-gray-100">
      <h3>Slack authorization for Endatix Bot</h3>
      {message}<br />
    </div>
  );
}