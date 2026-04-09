import { randomBytes } from "node:crypto";

export function randomBase64(bytes) {
  return randomBytes(bytes).toString("base64");
}

export function randomHex(bytes) {
  return randomBytes(bytes).toString("hex");
}

export function randomSigningKey(length) {
  const alphabet =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_!@#$%^&*+=";
  const bytes = randomBytes(length);
  let key = "";
  for (let i = 0; i < length; i += 1) {
    key += alphabet[bytes[i] % alphabet.length];
  }
  return key;
}
