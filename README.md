# First Bench: School Admission System

A school opens classes with a fixed number of seats. A guardian applies with the student's 17 digit birth registration number, gets the seat on hold for 10 minutes, and pays the admission fee before the time ends. If they do not pay in time, the seat goes back to the class for someone else.

Built as a take-home assignment. Backend in ASP.NET Core with PostgreSQL, frontend in React, all started with one Docker command.

## Run it

You need Docker Desktop.

```bash
docker compose up
```

- App: http://localhost:3000
- API: http://localhost:8080 (try http://localhost:8080/classes)

The first run builds the images, which takes a few minutes. A fresh database is empty: open "স্কুল অফিস" (School office) and create a class first.

`docker compose down` stops everything and keeps the data. `docker compose down -v` also deletes the database volume.

## Settings

Everything works with no `.env` file. To change something, copy `.env.example` to `.env` and edit it.

| Variable | Default | What it does |
|---|---|---|
| `HOLD_DURATION_SECONDS` | `600` | How long a seat stays on hold |
| `CORS_ALLOWED_ORIGINS` | `*` | Which web origins may call the API |
| `API_BASE_URL` | `http://localhost:8080` | Where the frontend finds the API, read when the container starts |
| `FRONTEND_PORT` | `3000` | Host port for the frontend |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | local demo values | Database name and login. The defaults are for local use only. |

## Run the tests

```bash
docker compose -f docker-compose.tests.yml run --rm backend-tests
```

```bash
docker compose -f docker-compose.tests.yml run --rm frontend-tests
```

```bash
docker compose -f docker-compose.tests.yml down
```

The backend tests start the real API in memory against a throwaway PostgreSQL, with a new database for each test class. They include 300 students applying for a 10 seat class at the same moment, and two API instances sharing one database.

## Check the rules yourself

**A hold expires by itself.** Start with a 30 second hold, apply, and watch the countdown reach zero. The page shows EXPIRED and the seat appears free again in the class list, with no reload.

```bash
HOLD_DURATION_SECONDS=30 docker compose up
```

On Windows PowerShell: `$env:HOLD_DURATION_SECONDS=30; docker compose up`

**Two backends, one seat limit.** A second API process runs on port 8081 against the same database.

```bash
docker compose --profile two-backends up
```

**Data survives a restart.** Run `docker compose down`, then `docker compose up`. The classes and applications are still there.

## If a port is busy

- Port 3000: set `FRONTEND_PORT=3001` and open http://localhost:3001.
- Port 8080: change the backend line in `docker-compose.yml` to `"8090:8080"` and set `API_BASE_URL=http://localhost:8090`.

## How it works

- Seats are never stored as a number. Free seats are counted from the holds each time, and a hold whose time has passed stops counting at that instant. No background job is needed.
- Applying locks the class row (`select ... for update`) inside one transaction, counts the taken seats and inserts the hold. Every request for that class waits its turn at the database, so the limit holds even with two backend processes.
- All times come from the database clock. The frontend keeps the difference between the server clock and the device clock, so the countdown is right even when a phone's clock is wrong.

More detail is in [ASSUMPTIONS.md](ASSUMPTIONS.md).

## Layout

```
backend/
  src/SchoolAdmission.Api/     controllers, services with hand-written SQL, schema file
  tests/SchoolAdmission.Api.Tests/
frontend/
  src/features/                classes, applications, school office, number box
  src/i18n/                    Bangla and English words
  nginx/                       server config and runtime API address
  tests/
docker-compose.yml             postgres, backend, frontend
docker-compose.tests.yml       backend and frontend tests
```

## Stack

- .NET 10, ASP.NET Core controllers, Npgsql and Dapper, PostgreSQL 17
- React 19, TypeScript, Vite, Tailwind CSS 4, TanStack Query, React Router
- xUnit for the backend, Vitest and Testing Library for the frontend
- nginx (unprivileged image) serving the frontend

## License

MIT. See [LICENSE](LICENSE).
