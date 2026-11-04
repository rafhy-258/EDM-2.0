# EDM 2.0 web application

Phase 0 shell. Built with React 19, strict TypeScript, Vite 8, Fluent UI React v9 (ADR-0003), TanStack Table v8 and Tailwind CSS (layout utilities only, preflight disabled).

## Scripts

| Command | What it does |
|---|---|
| `npm ci` | Installs exactly the locked dependencies |
| `npm run dev` | Dev server on port 5173, bound to 0.0.0.0. `/api` is proxied to `EDM_API_ORIGIN` (default `http://localhost:5000`) |
| `npm run typecheck` | `tsc --noEmit` with strict options |
| `npm run lint` | ESLint with typescript-eslint (strict) and react-hooks rules |
| `npm test` | Vitest with jsdom, Testing Library and axe-core |
| `npm run build` | Typecheck, then production build to `dist/` |
| `npm run licences` | Fails if any production dependency has a licence outside the allow-list |

## Structure

```
src/
  app/          root component, routing and route guards
  audit/        Activity Logs viewer (Part 10.4): API client, Karachi date helpers, table, detail dialog
  components/   shell, status badges, state views (loading, empty, error, no permission), data table, dialogs
  design/       layout and spacing tokens (colour comes from Fluent themes)
  navigation/   Part 14.1 navigation data, and the pure permission filter
  pages/        placeholder and not-found pages
  session/      permission catalogue (Part 20.1), role fixtures (Part 20.2), session context
  status/       one label and colour per status (Part 8.1)
  test/         test setup
```

## Rules for this code

- **Server is the authority.** Permission checks here decide what is shown, never what is allowed. Each API call is authorised on the server.
- **P2 items are never rendered.** `visibleNavigation` removes them and a test asserts it.
- **Status is never colour alone.** Every badge shows its text label.
- **Role fixtures are development only.** `devSessionFor` and the role switcher are used only when `import.meta.env.DEV` is true. Production has no fixture path. The session comes from the server (`GET /api/v1/auth/session`), and without one the sign-in page is shown. The sign-in page and fixture path are covered by `src/pages/SignInPage.test.tsx` and `src/app/SessionFlow.test.tsx`.
- **No localhost in browser code.** Browser code calls relative `/api` URLs only.
