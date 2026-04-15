import { randomBytes } from "node:crypto";

export function randomBase64(bytes) {
  return randomBytes(bytes).toString("base64");
}

export function randomHex(bytes) {
  return randomBytes(bytes).toString("hex");
}

export function randomSigningKey(length) {
  if (!length || length <= 0) return "";

  const alphabet =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_!@#$%^&*+=";
  const alphabetLength = alphabet.length;

  // Calculate the largest multiple of alphabetLength less than 256
  // to avoid modulo bias.
  const maxValid = Math.floor(256 / alphabetLength) * alphabetLength;
  
  let key = "";

  while (key.length < length) {
    const bytes = randomBytes(Math.max(length, 32));

    for (const byte of bytes) {
      if (byte < maxValid) {
        key += alphabet[byte % alphabetLength];
      }

      if (key.length === length) {
        break;
      }
    }
  }
  return key;
}