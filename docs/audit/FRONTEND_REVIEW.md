# Frontend review · M0

## Current design

| Area | Observed behavior |
| --- | --- |
| Stack and structure | React 19, strict TypeScript, Vite, Tailwind 4, shadcn/Radix, TanStack Query, React Router, SignalR, and cmdk. Feature folders hold API calls, pages, and related components. |
| Appearance | `src/index.css` defines light and dark OKLCH tokens, semantic status colors, and Geist typography. |
| State | TanStack Query manages server data. Task filters are synchronized with the URL. |
| API | `shared/api/http.ts` handles the base URL, bearer token, Problem Details responses, and a single refresh retry on 401. |
| Realtime | Task detail listens for refresh hints and re-reads persisted records from the API. List pages poll. |
| Navigation and states | Routes target implemented features. Feature pages use loading, empty, and error states with retry where applicable. Dashboard counts come from the API. |
| Accessibility | Form labels, visible focus rings, a skip link, Radix controls, and reduced-motion handling. |
| Test coverage | 23 Vitest tests cover components, hooks, and utilities. The validation run and manual coverage are recorded in the [source audit](SOURCE_CODE_AUDIT.md#verification). |

## Findings

| ID | Priority | Evidence and impact | Remediation or acceptance |
| --- | --- | --- | --- |
| FE-001 | Medium | CI runs Vitest and does not run the real dashboard against the API. UI integration and mobile layout regressions can escape CI. | Add a Playwright Compose job covering sign-in, temporary password change, project and repository setup, task submission, approval, and cancellation. Check overflow and accessibility. |
| FE-002 | Low · fixed | The approval badge previously reused the neutral pending task appearance. | `ApprovalStatusBadge.tsx` now uses an approval-specific label and warning tone. Its component test covers the display. |
| FE-003 | Low | Task and approval lists poll every 15 seconds. The frontend only subscribes to individual tasks. | After SEC-010, wire authorized project subscriptions and retain polling for recovery. |
| FE-004 | Low | Dialogs use `FormData` and display server-side validation details. Current forms are small. | Use a shared schema for larger nested agent and workflow forms when those are built. |
| FE-005 | Low | `/tasks` exposes filtering and pagination. The table and API do not support selecting a sort order. | Add explicit API and table sorting when the task list requires user-controlled ordering. |
| FE-006 | Info | Agent Studio, provider management, tool registry, workflow studio, execution inspector, and usage views depend on backend work. | Track the screens with the corresponding [roadmap milestones](../IMPLEMENTATION_ROADMAP.md). |
| FE-007 | Info | The review inspected semantic colors, and the project has no automated contrast checks. | Include accessibility and contrast checks in FE-001. |
| FE-008 | Low · fixed | Vite requested production source maps. The Tailwind build plugin emitted `SOURCEMAP_BROKEN`, and nginx served the generated artifacts. | Restore Vite's default for production bundles. Confirm `npm run build` is warning-free and the output contains no `.map` files. |
| FE-009 | Low | The desktop sign-in page promises sandboxed fixes and completed PRs, while the current Planner stops at the Coding placeholder. | Describe the planning pilot in the sign-in copy until a verified PR can be produced. |
