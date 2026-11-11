# AI usage

## In short

| | |
|---|---|
| **Tools** | Claude Code (Anthropic, Claude Opus 5.5 model) wrote the code, tests and first drafts of the docs. Other AI assistants helped me plan and review. |
| **My part** | The requirements, the design of the seat rules, the API contract, the test plan, every review, every run, and the final call on each change. |
| **How I checked** | Tests against a real PostgreSQL, a second AI review, database checks after stress tests, and the app run in Docker and used in a browser. |

I treated the AI like a fast junior developer. It writes a lot of code quickly. It also makes confident mistakes. So the plan came first, and nothing was trusted until it was run.

---

## How I worked

**1. Spec before code.** Before any code, I wrote a build brief. It fixed the parts that are easy to get wrong:

- the API contract, field by field, and an error code for every failure
- the order of checks: 404, then 403, then the status
- the seat rule: one transaction, a row lock on the class, seats counted and never stored
- the time rule: every time check uses the database clock
- the list of tests that must exist, starting with the concurrency test
- the screens, the Bangla and English wording, and what must never be built (login, seed data, a rate limiter)

**2. Small steps.** The AI built one part at a time with a commit for each. I read the code before moving on.

**3. Independent review.** Before the first run, I gave the code to a second AI assistant as a strict reviewer. It found three real bugs (listed below).

**4. Prove it.** The backend was built and tested in Docker against PostgreSQL 17. Then I asked for harder tests: a crowd of students clicking many times on two backends at once, and payments sent in the exact second a hold ends. After each run the tests check the database itself with SQL.

**5. Use it.** I opened the app in a browser at phone and desktop sizes, in both languages, and sent back what looked wrong.

---

## Decisions I made, and why

| Decision | Why |
|---|---|
| Lock the class row (`select ... for update`) inside one transaction | Two requests cannot both see "one seat left". The lock is in PostgreSQL, so it also works with two backend processes. |
| Count seats from holds, never keep a counter | A counter needs a background job to give back expired seats. A count is always right the moment a hold expires. |
| `clock_timestamp()` for every time check | `now()` is fixed at the start of a transaction, so it is wrong after waiting for a lock. One clock also means two servers cannot disagree. |
| Dapper with plain SQL | Each seat rule is one SQL statement I can read and explain. No hidden queries from an ORM. |
| A partial unique index on active holds | A second safety net under the "one active application per class" rule. |
| Retry once on lock timeout or deadlock, then 503 | A busy database should slow people down, never give a 500. |
| Student id only in the `X-Student-Id` header | It never ends up in a URL, a browser history or a server log. Logs keep only the last 4 digits. |
| Countdown from the absolute expiry time plus the server clock offset | A sleeping tab or a wrong phone clock cannot make the countdown lie. |
| API address read when the container starts | Vite fixes variables at build time. A runtime file lets the API URL change without a rebuild. |

---

## Where the AI was wrong, and how it was caught

| # | What was wrong | How it showed up | Caught by | Fix |
|---|---|---|---|---|
| 1 | A quick "class is full" check ran before checking the student's own hold. A student who already held the last seat got CLASS_FULL instead of ALREADY_APPLIED. | Reading the code against the check order in my brief | The AI, while writing tests | The check now lets the locked transaction answer. A test covers it. |
| 2 | A request body of just `null` would have caused a 500 on `POST /classes`. | Writing the bad input test | The AI | A null body is treated as an empty form and gets a 400. |
| 3 | The countdown used the phone's clock for its first second. | A frontend test failed | The test | A new server time redraws every countdown at once. |
| 4 | Packages were first installed with a Node version that was too old, so the test runner could not start. | The test runner failed to start | The failed run | Reinstalled with Node 22, the same as the Docker image. |
| 5 | The database connection pool was never closed, so each test host left 40 connections open. | Code review | The second AI review I ran | The container now owns the pool and closes it. |
| 6 | Server time was cut to whole seconds, and the frontend replaced its clock estimate on every answer. An expired hold could show "on hold" again for a moment. | Code review | The second AI review I ran | Server time has milliseconds. The frontend keeps its best estimate. |
| 7 | Two rare paths in confirm and cancel could still return a 500. | Code review | The second AI review I ran | They retry once, then return 409 HOLD_EXPIRED. |
| 8 | Paying did not take the class lock. A payment in the exact second a hold ended could race with a new applicant, and one seat could go to two students. | Writing the harder stress tests I asked for | The AI, after I asked for tests that rule out double booking | Paying now takes the same lock. A stress test checks the database after every round. |
| 9 | The AI added stricter class name rules than the assignment asks for (a minimum length, at least one letter). | My review | Me | Removed. An automated API test could send a valid name that the extra rule rejects. |
| 10 | On a phone, the sticky header covered half the screen, and the 17 digit number did not fit. | Browser check at 360 px | The browser check | The header scrolls away on phones. The number box uses a smaller font. |
| 11 | The first design looked generic, with a colour close to many AI-made sites. | My review | Me | A new palette with one red accent, a dark header, dark mode and a school doodle background. |
| 12 | The first README draft said the project was made for a university course. | Re-reading the brief | The AI | Fixed before the first commit. |

---

## What I learned

- **Write the hard rules down first.** The bugs that mattered were in places the brief marked as risky: locking, time and error order.
- **A green test is only as good as its question.** The payment race (#8) passed every normal test. It showed up only when I asked for a test aimed at that exact moment.
- **Check the database, not just the API.** The stress tests end by asking PostgreSQL directly whether any class has more seats in use than it has.
- **Use a second reviewer.** Three of the bugs above were found by a review the first AI did not do on its own code.
