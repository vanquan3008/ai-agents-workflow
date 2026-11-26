# Security review · M0

The review covered backend authorization, dashboard session handling, and Docker, nginx, and CI configuration. Evidence comes from source review, automated tests, and the isolated development stack.

## Controls verified in source

| Control | Evidence |
| --- | --- |
| HS256 access tokens with issuer, audience, and lifetime validation | `Api/Auth/AuthExtensions.cs` restricts accepted algorithms and checks the user's security stamp on JWT validation. |
| Password changes, role changes, and deactivation invalidate subsequent authenticated requests | `AuthExtensions.OnTokenValidated` and `Domain/Users/User.cs`. See SEC-010 for existing realtime subscriptions. |
| Rotating refresh tokens with hashed storage and reuse detection | `Features/Auth/RefreshSession.cs`, `Domain/Users/RefreshToken.cs`, and `AuthTests`. |
| Refresh cookie and CSRF controls | `Api/Endpoints/AuthEndpoints.cs` uses HttpOnly, `SameSite=Strict`, a scoped path, and a required custom header for refresh and logout. |
| Access tokens remain in browser memory | `frontend/src/features/auth/session.ts`. |
| Password hashing and login defenses | `Pbkdf2PasswordHasher` uses PBKDF2-HMAC-SHA512 with 210,000 iterations and fixed-time comparison. Login does dummy verification for unknown accounts. Account lockout and rate limits cover login and refresh. Task creation is rate limited per user. |
| Project permissions are checked on reads and subscription requests | `ProjectAccess` and `AuthorizeTaskSubscription` enforce database-backed checks and record denials. |
| Human approval and code-owned invariants | The Worker has no user identity. `DecideApproval`, `PlanningStep`, and `ChangePolicyEvaluator` enforce permissions and policy in code. |
| Model inputs and budgets | `PlannerAgent` marks supplied issue text and repository paths as untrusted. `BudgetGuard` blocks calls without a configured price. |
| Secret and error handling | `LogEnrichers` redact recognized sensitive properties and token patterns. `GlobalExceptionHandler` omits stack traces from API errors. |
| Deployment safeguards | Production configuration rejects fake adapters and requires an encryption key. Backend and frontend images use unprivileged users. |
| Query handling | Read queries use parameterization and escape wildcards in literal `ILIKE` searches. |

## Findings

| ID | Priority | Evidence and impact | Remediation or acceptance |
| --- | --- | --- | --- |
| SEC-001 | Medium · fixed | nginx locations that set cache headers previously dropped inherited security headers. | The locations now include the security headers. Check `X-Frame-Options` and `X-Content-Type-Options` on the document, assets, and runtime config. |
| SEC-002 | Medium · fixed | SignalR passes an access token in the WebSocket query string. The default nginx request log includes the query. | Access logging is disabled for `/hubs/`. API request logging records the path. Check realtime traffic leaves no access token in the logs. |
| SEC-003 | Medium | `frontend/nginx.conf` has no Content Security Policy, leaving fewer browser safeguards if a script injection defect is introduced. | Add and verify a restrictive policy for same-origin scripts and assets, SignalR, and Radix's inline positioning styles. Check browser violations in the end-to-end suite. |
| SEC-004 | Medium · fixed | The previous PostgreSQL port mapping listened on all interfaces. | Compose now publishes the port on IPv4 loopback. Verify the binding with `docker compose port postgres 5432`. |
| SEC-005 | Medium · future | The Anthropic key comes from configuration. `ISecretProtector` is registered for future database-backed credentials. | Encrypt credentials before persistence in M2. API responses may return credential presence. Verify API output, logs, and storage contain no plaintext key. |
| SEC-006 | Medium · future | `UnavailableSandboxManager` is a placeholder. | Before enabling file writes or command execution, verify unprivileged users, resource and time limits, restricted networking, path and symlink containment, bounded output, and approval for high-risk actions. Keep host mounts and the Docker socket out. |
| SEC-007 | Low | `AuthOptions.Jwt.SigningKey` holds one key. Changing it invalidates current access tokens. | Add a key ring with a `kid` and an overlap window when scheduled rotation is required. |
| SEC-008 | Low | Denial audit uses the request's unit of work. | See SRC-011 for the transactional footgun and its regression case. |
| SEC-009 | Low | Sign-in uses email and password. The identity model names providers without an active SSO flow. | Evaluate OIDC and MFA before organizational rollout. |
| SEC-010 | High | `TasksHub` checks permission when joining a task or project group. The fan-out uses existing group membership. Removing a member or deactivating an account does not remove already joined connections, and the hub mapping does not close connections when the access token expires. | Invalidate active subscriptions when access is revoked and enforce token expiry for realtime connections. Add a defensive integration test that checks event delivery stops after authorization changes. |
| SEC-011 | Medium | The authentication limiter keys requests by the API's remote IP. nginx forwards the original address, and `Program.cs` has no forwarded-header middleware. Users coming through the same nginx instance share a bucket across sign-in, password change, and refresh. Dashboard reloads during browser checks hit HTTP 429. | Apply forwarded headers only from trusted proxies and review the shared endpoint limit. Verify that distinct clients receive independent limits through nginx. |

## Model output boundary

The Planner places user and repository input in a delimited user message. A proposed plan can affect what the system evaluates. The backend verifies paths, decides approval requirements, and applies the resulting state transitions. Future tool calls require schema validation and authorization, with returned content treated as untrusted input.
