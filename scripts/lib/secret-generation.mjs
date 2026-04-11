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
  const alphabetLength = alphabet.length;
  const maxValid = Math.floor(256 / alphabetLength) * alphabetLength;
  const bytes = randomBytes(length * 2);
  let key = "";
  let pos = 0;
  while (key.length < length) {
    const byte = bytes[pos++];
    if (byte < maxValid) {
      key += alphabet[byte % alphabetLength];
    }
  }
  return key;
}
