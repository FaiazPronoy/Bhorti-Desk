import { describe, expect, it } from "vitest";
import { bn } from "../src/i18n/bn";
import { en } from "../src/i18n/en";

describe("dictionaries", () => {
  it("have exactly the same keys in Bangla and English", () => {
    expect(Object.keys(bn).sort()).toEqual(Object.keys(en).sort());
  });

  it("have no empty text", () => {
    for (const [key, text] of [...Object.entries(en), ...Object.entries(bn)]) {
      expect(text.trim(), key).not.toBe("");
    }
  });

  it("use the same {placeholders} in both languages", () => {
    const placeholders = (text: string) => (text.match(/\{\w+\}/g) ?? []).sort();
    for (const key of Object.keys(en) as (keyof typeof en)[]) {
      expect(placeholders(bn[key]), key).toEqual(placeholders(en[key]));
    }
  });

  it("use the exact button words from the assignment in Bangla", () => {
    expect(bn["classes.apply"]).toBe("আবেদন করুন");
    expect(bn["application.pay"]).toBe("ভর্তি ফি দিন");
    expect(bn["application.cancel"]).toBe("বাতিল");
  });

  it("show the status code next to every status word", () => {
    for (const status of ["HELD", "CONFIRMED", "EXPIRED", "CANCELLED"] as const) {
      expect(en[`status.${status}`]).toContain(`(${status})`);
      expect(bn[`status.${status}`]).toContain(`(${status})`);
    }
  });
});
