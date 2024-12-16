"use client";

import { useSearchParams } from "next/navigation"; // Use `next/navigation` instead of `next/router`
import { useEffect, useState } from "react";
import { getSlackBearerToken, sendSlackBearerToken } from "@/services/integrations";
import { SlackAuthResponse } from "@/types";

export default async function Slack() {
  const searchParams = useSearchParams(); // To get query params
  const code = searchParams.get("code");
  var message = "";

  var responseJson = await getSlackBearerToken(code ?? "");

  if(responseJson.ok) {
    await sendSlackBearerToken(responseJson.access_token);

  }
  else {
    message = responseJson.error;
  }

  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-gray-100">
      <h1>Slack authorization for Endatix Bot</h1>
      {message}<br />
    </div>
  );
}