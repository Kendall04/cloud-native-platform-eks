import crypto from "node:crypto";

const issuer = process.env.JWT_ISSUER;
const audience = process.env.JWT_AUDIENCE;
const secret = process.env.JWT_SECRET;
const proxySecret = process.env.PLATFORM_TRUSTED_PROXY_SECRET;

export const handler = async (event) => {
  try {
    validateConfiguration();

    const authorizationHeader =
      event?.headers?.authorization ??
      event?.headers?.Authorization ??
      event?.headers?.AUTHORIZATION;

    if (!authorizationHeader) {
      return deny("Authorization header was missing.");
    }

    if (!authorizationHeader.startsWith("Bearer ")) {
      return deny("Authorization header did not contain a bearer token.");
    }

    const token = authorizationHeader.slice("Bearer ".length).trim();

    if (!token) {
      return deny("Bearer token was empty.");
    }

    const payload = validateToken(token);
    const roles = extractRoles(payload);
    const userId = stringClaim(payload, "userId") ?? stringClaim(payload, "sub");
    const email = stringClaim(payload, "email") ?? "";

    if (!userId) {
      return deny("Validated token did not contain a user identifier.");
    }

    return {
      isAuthorized: true,
      context: {
        userId,
        email,
        roles: roles.join(","),
        proxySecret,
      },
    };
  } catch (error) {
    console.error("JWT authorizer denied the request", {
      message: error instanceof Error ? error.message : String(error),
      routeKey: event?.routeKey,
      requestId: event?.requestContext?.requestId,
    });

    return deny("JWT validation failed.");
  }
};

function validateConfiguration() {
  if (!issuer) {
    throw new Error("JWT_ISSUER is required.");
  }

  if (!audience) {
    throw new Error("JWT_AUDIENCE is required.");
  }

  if (!secret || secret.length < 32) {
    throw new Error("JWT_SECRET must be at least 32 characters long.");
  }

  if (!proxySecret || proxySecret.length < 32) {
    throw new Error("PLATFORM_TRUSTED_PROXY_SECRET must be at least 32 characters long.");
  }
}

function validateToken(token) {
  const segments = token.split(".");

  if (segments.length !== 3) {
    throw new Error("JWT did not contain three segments.");
  }

  const [encodedHeader, encodedPayload, encodedSignature] = segments;
  const header = JSON.parse(base64UrlDecode(encodedHeader));
  const payload = JSON.parse(base64UrlDecode(encodedPayload));

  if (header?.alg !== "HS256") {
    throw new Error(`Unsupported JWT algorithm '${header?.alg ?? "unknown"}'.`);
  }

  const signingInput = `${encodedHeader}.${encodedPayload}`;
  const expectedSignature = crypto
    .createHmac("sha256", secret)
    .update(signingInput)
    .digest();
  const actualSignature = base64UrlDecodeToBuffer(encodedSignature);

  if (
    expectedSignature.length !== actualSignature.length ||
    !crypto.timingSafeEqual(expectedSignature, actualSignature)
  ) {
    throw new Error("JWT signature validation failed.");
  }

  const now = Math.floor(Date.now() / 1000);
  const exp = numericClaim(payload, "exp");
  const nbf = numericClaim(payload, "nbf");
  const iss = stringClaim(payload, "iss");
  const aud = payload?.aud;

  if (!exp || exp <= now) {
    throw new Error("JWT has expired.");
  }

  if (nbf && nbf > now) {
    throw new Error("JWT is not valid yet.");
  }

  if (iss !== issuer) {
    throw new Error("JWT issuer was invalid.");
  }

  if (!matchesAudience(aud, audience)) {
    throw new Error("JWT audience was invalid.");
  }

  return payload;
}

function extractRoles(payload) {
  const roleClaims = [payload?.roles, payload?.role].flatMap((value) => {
    if (Array.isArray(value)) {
      return value;
    }

    if (typeof value === "string" && value.trim()) {
      return value.split(",").map((entry) => entry.trim());
    }

    return [];
  });

  return [...new Set(roleClaims.filter((value) => typeof value === "string" && value.trim()))];
}

function matchesAudience(claimValue, expectedAudience) {
  if (Array.isArray(claimValue)) {
    return claimValue.includes(expectedAudience);
  }

  return claimValue === expectedAudience;
}

function stringClaim(payload, name) {
  const value = payload?.[name];
  return typeof value === "string" && value.trim() ? value.trim() : null;
}

function numericClaim(payload, name) {
  const value = payload?.[name];
  return typeof value === "number" ? value : null;
}

function deny(reason) {
  return {
    isAuthorized: false,
    context: {
      denialReason: reason,
    },
  };
}

function base64UrlDecode(value) {
  return base64UrlDecodeToBuffer(value).toString("utf8");
}

function base64UrlDecodeToBuffer(value) {
  const normalized = value.replaceAll("-", "+").replaceAll("_", "/");
  const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "=");
  return Buffer.from(padded, "base64");
}
