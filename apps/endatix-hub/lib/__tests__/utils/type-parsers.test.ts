import { parseBoolean } from "@/lib/utils/type-parsers";
import { describe, expect, it } from "vitest";

describe("parseBoolean", () => {
  it("should return false for undefined input", () => {
    expect(parseBoolean(undefined)).toBe(false);
  });

  it("should return false for empty string", () => {
    expect(parseBoolean("")).toBe(false);
  });

  it("should return false for whitespace string", () => {
    expect(parseBoolean("   ")).toBe(false);
  });

  it('should return true for "true" (case-insensitive with or without whitespace)', () => {
    expect(parseBoolean("true")).toBe(true);
    expect(parseBoolean("TRUE")).toBe(true);
    expect(parseBoolean("True")).toBe(true);
    expect(parseBoolean("true ")).toBe(true);
    expect(parseBoolean(" true")).toBe(true);
    expect(parseBoolean(" true ")).toBe(true);
  });

  it('should return true for "1"', () => {
    expect(parseBoolean("1")).toBe(true);
    expect(parseBoolean("1 ")).toBe(true);
    expect(parseBoolean(" 1")).toBe(true);
    expect(parseBoolean(" 1 ")).toBe(true);
  });

  it("should return false for other string values", () => {
    expect(parseBoolean("yes")).toBe(false);
    expect(parseBoolean("on")).toBe(false);
    expect(parseBoolean("false")).toBe(false);
    expect(parseBoolean("0")).toBe(false);
    expect(parseBoolean("no")).toBe(false);
    expect(parseBoolean("off")).toBe(false);
  });
});
