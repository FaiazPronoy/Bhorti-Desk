# Security review

Checked on 24 November 2026 against the OWASP Top 10 (2021) and OWASP ASVS 4.0 level 1. This is a self review of a portfolio project, not an audit.

## How to run the checks

```bash
dotnet list backend/SchoolAdmission.sln package --vulnerable --include-transitive
cd frontend && npm audit --omit=dev
```

Both reported no known vulnerable packages on 24 November 2026. CI runs them on every push.

## Findings by category

| OWASP 2021 | What is in place | Open points |
|---|---|---|
| A01 Broken access control | Every office endpoint checks a staff session. Applications, documents, slips and notices check that X-Student-Id owns the hold; with `SIGN_IN_REQUIRED=true` the signed-in guardian must own that student too. Documents are served only by an endpoint that checks owner or staff, never from a public path. Each staff view of a document is written to the audit log. | With sign-in off (the default, to keep the original contract), anyone who knows a birth registration number can act for that student. This is the assignment's rule; turn sign-in on for real use. |
| A02 Cryptographic failures | OTPs and session tokens are stored only as SHA-256 hashes. Staff passwords use PBKDF2. Slip codes and payment callbacks use HMAC-SHA256, compared in constant time. | `PAYMENT_SANDBOX_SECRET` and `SLIP_SECRET` have a local default and the app logs a warning while they do. Set both in any shared deployment. TLS is the hosting platform's job. |
| A03 Injection | All SQL is parameterised through Dapper. No HTML from the server is put into the page; the QR is drawn from booleans. CSV exports quote every field and neutralise formulas such as `=HYPERLINK(...)`. | None found. |
| A04 Insecure design | Seat counts are counted, never stored. Payment callbacks are idempotent. A paid-too-late callback is marked LATE and never takes a seat. Newcomers cannot take a seat while the waiting list has people. | None found. |
| A05 Security misconfiguration | nginx sends CSP (`default-src 'self'`), `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy`, `Cross-Origin-Opener-Policy`, `nosniff`. The API sends no `Server` header and never sends exception text. Request bodies are limited to 16 KB, uploads to about 1 MB. | HSTS is not set by the app because it runs on plain http locally; set it at the TLS terminator. |
| A06 Vulnerable components | Audits above; versions pinned in the lock files. | Images use moving tags such as `postgres:17-alpine`; pin digests for production. |
| A07 Identification and authentication | OTP: 6 digits, 5 minutes, 5 tries, one per minute, five per hour. Staff login answers the same way for a wrong password and an unknown email. Session cookie: HttpOnly, SameSite=Strict, Secure behind HTTPS. Signed-in writes need the `X-Bhorti-Request` header (CSRF). | Rate limits are off by default for the original contract; set `RATE_LIMITS_ENABLED=true` in production. |
| A08 Software and data integrity | Payment callbacks are signed and checked for amount; uploads are checked by their first bytes, not by name or claimed type. | None found. |
| A09 Logging and monitoring | JSON logs with a request id on every line, birth registration numbers masked to the last four digits, phone numbers to the last three, sign-in codes never logged by the demo SMS provider. Audit log of every office action, filterable and exportable. `/health`, `/ready`, `/metrics`. | No alerting; that belongs to the hosting platform. |
| A10 Server-side request forgery | The API makes no outgoing requests from user input. The sandbox callback is handled in process. | None found. |

## Children's data

- Collected: names in Bangla and English, date of birth, guardian name and mobile, previous school, a photo and a birth certificate. Nothing else.
- The birth registration number is masked to the last four digits everywhere on screen, in logs, on the slip and on the public verify page. The number box shows dots until the guardian presses Show.
- Uploaded files are stored in the database with a random id; the original file name is not kept.
