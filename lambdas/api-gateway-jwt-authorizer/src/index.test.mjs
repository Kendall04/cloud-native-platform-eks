import assert from "node:assert/strict";
import test from "node:test";

import { __testing } from "./index.mjs";

test("extractSecretValue accepts a plain Secrets Manager string", () => {
  assert.equal(__testing.extractSecretValue("plain-secret-value", "test secret"), "plain-secret-value");
});

test("extractSecretValue accepts a JSON value field", () => {
  assert.equal(__testing.extractSecretValue('{"value":"json-secret-value"}', "test secret"), "json-secret-value");
});

test("extractSecretValue rejects JSON without a supported string field", () => {
  assert.throws(
    () => __testing.extractSecretValue('{"unexpected":"shape"}', "test secret"),
    /did not contain a supported string field/,
  );
});
