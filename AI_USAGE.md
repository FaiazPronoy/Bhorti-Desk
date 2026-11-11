# AI usage

## In short

| | |
|---|---|
| **Tools** | Claude Code (Anthropic, Claude Opus 5.5) wrote the code, the tests and the first drafts of the documents. Claude in chat helped me turn my requirements into a build brief and later reviewed the code. Several other AI assistants, Gemini among them, reviewed the brief. |
| **My part** | Reading the assignment, setting the requirements and the scope, choosing between the options the AI offered, organising the reviews, deciding what to accept, running the app, and the final call on every change. |
| **How it was checked** | Six independent reviews of the plan, one independent review of the code, tests against a real PostgreSQL, SQL checks on the database after stress tests, and the app used in a browser. |

C# and PostgreSQL were new to me when I started. So I did not trust one AI's answer on anything that mattered. My rule was simple: **one AI writes, a different one reviews, and a test decides.**

---

## How I worked

**1. Requirements first.** I read the assignment and wrote down what I wanted: every rule in it covered, Bangla and English, easy for a guardian on a phone, clear names for files and tables, and a design that does not look like a template. I also fixed the scope: only what the assignment asks, no login, no seed data.

**2. A written brief before any code.** With an AI assistant I turned this into a build brief: the API contract field by field, an error code for every failure, the seat rule, the time rule, the list of tests, the screens and the wording in both languages. Every line of the assignment got an ID so nothing could be dropped.

**3. Six reviewers for the plan.** I gave the assignment and the brief to six AI assistants separately, with the same strict review prompt: find what is missing, find a race, find what a hidden test would break, say what to cut. I then merged their answers. Where they disagreed, I picked a side and wrote the reason in `ASSUMPTIONS.md`. This step found seven mistakes in the first plan before a single line of code existed (table below).

**4. Small steps.** Claude Code built one part at a time, with a commit for each.

**5. A second reviewer for the code.** Before the first run I gave the code to a different AI assistant as a strict reviewer, with a list of exact questions (can seats ever be oversold, can any bad input return a 500, do the tests test what their names say). It found three real bugs.

**6. Prove it.** The backend was built and tested in Docker against PostgreSQL 17. Then I asked for harder tests: a crowd applying on two backends at once, and payments sent in the exact second a hold ends. After each run the tests ask the database directly whether any class has more seats in use than it has.

**7. Use it.** I opened the app in a browser at phone and desktop sizes, in both languages, and sent back what looked wrong.

---

## Decisions, and why

The AI proposed most of these. I chose them over the alternatives after the reviews, and I can explain each one.

| Decision | Why |
|---|---|
| Lock the class row (`select ... for update`) inside one transaction | Two requests cannot both see "one seat left". The lock lives in PostgreSQL, so it also works with two backend processes. |
| Count seats from holds, never keep a counter | A counter needs a background job to give back expired seats. A count is right the moment a hold expires. |
| `clock_timestamp()` for every time check | `now()` is fixed at the start of a transaction, so it is stale after waiting for a lock. One clock also means two servers cannot disagree. |
| Dapper with plain SQL, not a full ORM | Each seat rule is one SQL statement I can read and explain. I know SQL well and was new to C#, so this was the honest choice. |
| A partial unique index on active holds | A second safety net under "one active application per class". |
| No background sweeper, no rate limiter | Neither is needed for the rules. Code that is switched off is only more to get wrong. |
| Class names are not unique | A test suite that creates the same class on every run would fail on its second run. |
| Bangla first, with an English switch | The assignment gives its button labels in Bangla, and the users are guardians. |
| API address read when the container starts | Vite fixes variables at build time. A runtime file lets the URL change without a rebuild. |

---

## Where the AI was wrong, and how it was caught

### In the plan, before any code

| # | What was wrong | Caught by |
|---|---|---|
| 1 | Confirm and cancel used `now()` while apply used `clock_timestamp()`. A confirm that started just before expiry could succeed after it. | Plan review (several reviewers) |
| 2 | The seat count inside the lock ignored expiry time, so a class could answer "full" while a seat had just become free. | Plan review |
| 3 | The nginx config had no fallback to `index.html`. A page refresh on an application would have been a 404, breaking the "refresh keeps your application" rule. | Plan review |
| 4 | The compose file did not pass `HOLD_DURATION_SECONDS` through, so nobody could have changed the hold time. | Plan review |
| 5 | Endpoints with no body would have rejected a request that had no `Content-Type`. | Plan review |
| 6 | Ids were typed as `Guid` in routes, so the framework would answer a bad id itself, with its own body. | Plan review |
| 7 | Error responses would have lost their CORS headers, so the browser would show "class full" as a network error. | Plan review |

### In the code

| # | What was wrong | Caught by | Fix |
|---|---|---|---|
| 8 | A quick "class is full" check ran before the student's own hold was looked at, so a student holding the last seat got CLASS_FULL where ALREADY_APPLIED was right. | Claude Code, while writing tests | The locked transaction now answers. A test covers it. |
| 9 | A body of just `null` would have caused a 500 on `POST /classes`. | The bad input test | Treated as an empty form, 400. |
| 10 | The countdown used the phone's clock for its first second. | A failing frontend test | A new server time redraws every countdown. |
| 11 | The connection pool was never closed, so each test host left 40 connections open. | The second AI's code review | The container owns the pool and closes it. |
| 12 | Server time was cut to whole seconds and the frontend replaced its clock estimate on every answer, so an expired hold could show "on hold" again for a moment. | The second AI's code review | Milliseconds on the server, best estimate kept on the client. |
| 13 | Two rare paths in confirm and cancel could return a 500. | The second AI's code review | Retry once, then 409. |
| 14 | **Paying did not take the class lock.** A payment in the exact second a hold ended could race with a new applicant, and one seat could go to two students. Every normal test passed. | Claude Code, after I asked for a test aimed at that exact second | Paying takes the same lock. A stress test checks the database after every round. |
| 15 | The AI added class name rules the assignment never asks for (a minimum length, at least one letter). A valid name from an automated test could have been rejected. | The second AI's review, near the end. I approved removing them. | Removed. |
| 16 | On a phone the sticky header covered half the screen and the 17 digit number did not fit. | Browser check at 360 px | The header scrolls away on phones. |
| 17 | The first design looked generic. | My own review | New palette, dark header, dark mode. |
| 18 | The first README draft described the project wrongly. | A re-read against the brief | Fixed before the first commit. |

---

## What I learned

- **The dangerous bugs were all about time and locking.** Items 1, 2, 12 and 14 are the same kind of mistake in four places. Next time I would write the stress test for the expiry second first.
- **A green test is only as good as its question.** The payment race (14) passed everything until a test was aimed at that moment.
- **Reviewers disagree, and that is useful.** Six reviewers gave different answers on three status codes. Having to choose made me understand the trade-off, and the choice is written in `ASSUMPTIONS.md`.
- **Check the database, not just the API.** The stress tests end by asking PostgreSQL itself.
- **Where I still depend on the AI.** It wrote the C#. I can explain the design, the SQL and why each rule holds, and I am still learning the finer points of ASP.NET Core.
