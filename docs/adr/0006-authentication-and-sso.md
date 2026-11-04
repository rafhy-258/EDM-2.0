# ADR-0006: Authentication: local accounts, OIDC and SAML, MFA hooks

**Status:** Accepted for design. The provider choice is Part 18 item 25 and is pending.
**Date:** 2026-11-04

## Decision
- ASP.NET Core Identity for local accounts, with password hashing, lockout after repeated failures, a password policy and session timeout.
- OpenID Connect through the built-in ASP.NET Core OIDC handler. This covers Entra ID (Azure AD), ADFS (OIDC), Okta and similar providers.
- SAML 2.0 through a SAML library that must pass the licence check. Candidates are evaluated in Phase 0. No commercial-only licence is acceptable without product-owner approval.
- AD/LDAP is used for directory synchronisation (users and groups) in Phase 1, not as an authentication path. Direct LDAP bind authentication is decided with the first bank (Part 18 item 25).
- MFA: hooks in the sign-in pipeline. TOTP is the basic option in Phase 1. Enforcement policy is Phase 2 (Part 14.1).
- Local accounts are the fallback when SSO is unavailable and are gated by configuration.
- Sign-in, sign-out, failed sign-in, lockout and MFA events are audited (Part 10.2).
- Session cookies are HttpOnly, Secure and SameSite. CSRF protection applies to every state-changing request.

## Consequences
- The Phase 0 SSO skeleton is testable with a local OIDC test server. The real provider is configured per deployment through environment variables.
