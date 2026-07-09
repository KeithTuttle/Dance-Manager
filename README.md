# DanceManager

Custom web app for managing dance classes across multiple studios. Desktop-first,
Linear-inspired UI (minimalist, sharp typography, structural borders, generous whitespace).

## Stack

| Layer     | Tech                                                        |
| --------- | ----------------------------------------------------------- |
| Backend   | .NET 8 Web API (C#), EF Core, Npgsql                        |
| Database  | PostgreSQL                                                  |
| Frontend  | Vue 3 (Composition API), Vite, Vue Router, Pinia            |
| UI        | Tailwind CSS + shadcn-vue (reka-ui), lucide-vue-next icons  |

## Layout

```
dance-manager/
├─ server/                       # .NET solution
│  └─ DanceManager.Api/
│     ├─ Models/                 # All domain entities + enums (shared contract)
│     ├─ Data/AppDbContext.cs    # DbContext, DbSets, model config
│     ├─ Controllers/            # StudiosController (others added per feature)
│     └─ Migrations/             # InitialCreate captures the full schema
├─ client/                       # Vite + Vue app
│  └─ src/
│     ├─ lib/api.ts              # shared axios instance (-> /api, proxied to :5184)
│     ├─ types/index.ts          # TS types mirroring backend models
│     ├─ stores/studio.ts        # global studio-selector (Pinia)
│     ├─ router/index.ts         # routes for all feature areas
│     ├─ components/layout/       # AppSidebar, PagePlaceholder
│     └─ views/                  # one view per feature area (stubs to build out)
└─ docker-compose.yml            # local Postgres
```

## Running locally

### One-click in VS Code

Open the repo folder and run the **`Start: client + API`** task (default build task —
`Ctrl+Shift+B`, or Terminal → Run Task). It launches the Vite dev server and the
API side-by-side in dedicated terminals. Recommended extensions are prompted on open
(Vue Volar, C# Dev Kit, Tailwind IntelliSense).

> The "Live Server" extension will **not** work here — it only serves static HTML and
> cannot compile Vue/TS. Use the Vite dev server (`npm run dev` / the task above).

### Manually

1. **Backend** — from `server/DanceManager.Api`: `dotnet run` (http://localhost:5184, Swagger at `/swagger`).
2. **Frontend** — from `client`: `npm run dev` (http://localhost:5199, proxies `/api` → backend).

### Database (optional for now)

The API **boots and runs without Postgres** — EF connects lazily, so the server starts
and `GET /api/health` returns `200` even with no database. DB-backed endpoints
(e.g. `/api/studios`) return `500` until Postgres is configured; the frontend tolerates
this and simply shows "No studios yet".

When ready to add real data:

1. `docker compose up -d db` (Postgres on :5432, db `dancemanager`).
2. From `server/DanceManager.Api`: `dotnet dotnet-ef database update`.

## Conventions for feature work

- **Entities & enums live in `server/.../Models`** and are the single source of truth.
  Mirror any additions in `client/src/types/index.ts`.
- **Controllers** follow `api/[controller]` routing; return the model types directly.
- **Enums** serialize as strings on both the wire and in the DB.
- **JSON columns** (`Formation.StudentCoordinates`, `Audition.SkillColumns`,
  `AuditionCandidate.Scores`) are `jsonb`, exposed as JSON strings.
- **Frontend** filters by the selected studio via `useStudioStore()`.
- **shadcn-vue components**: add with `npx shadcn-vue@latest add <name>` (config in `components.json`).
- After model changes: `dotnet dotnet-ef migrations add <Name>`.

## Feature areas (each a view + controller set)

1. Multi-Studio & Roster Management (`/roster`)
2. Attendance Tracker & Analytics (`/attendance`, `/analytics`)
3. Lesson Plans (`/lesson-plans`)
4. Choreography & Stage Formation Mapper (`/choreography`)
5. Recital Logistics & Production Ledger (`/recital`)
6. Mock Auditions (`/auditions`)
