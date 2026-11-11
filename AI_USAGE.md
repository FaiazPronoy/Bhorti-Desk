# AI usage

## The short version

| | |
|---|---|
| **Tools** | Claude Code (Anthropic, Claude Opus 5.5) wrote the code, the tests and the first drafts of the documents. Claude in chat helped me turn my needs into a build brief. It also reviewed the code later. Several other AI assistants reviewed the brief independently. |
| **My job** | I owned the design, the risks and the tests. The AI did most of the typing. |
| **What I did myself** | I read the assignment. I set the rules and the scope. I chose between the AI's options. I ran the reviews. I decided what to accept. I ran the app. I made the final call on every change. |
| **How I checked it** | Several independent reviews of the plan. One review of the code. Stress tests on a real PostgreSQL 17. SQL checks on the database after each test. The app used in a browser. |

C# and PostgreSQL were new to me. So I did not trust one AI on anything important.

My rule: **one AI writes, a different one reviews, and a test decides.**

---

## My working rules

1. **The invariant comes first.** Here it is: *seats in use (held + paid) never go over a class's capacity.* I judged every design choice against it.
2. **The author does not review its own work.** A different model reviews the code. It gets the spec and the code. It never gets the author's story.
3. **Nothing is true until a test says so.** "It should work" is not enough. Not from a person. Not from a model.
4. **Mistakes are written down as they happen.** The tables below come from that log.

---

## What I did as the engineer

The AI typed. I made the engineering calls. These are the main ones:

- **Rules before code.** I turned each line of the assignment into a rule with an ID. Nothing could be dropped.
- **Concurrency design.** I chose the row lock inside one transaction from the options. Two requests cannot both take the last seat. It also works with two backend processes.
- **Time design.** I settled on one clock for every time check: `clock_timestamp()` in the database. Two servers cannot disagree.
- **No counter.** I count seats from holds. A count is right the moment a hold expires. A counter needs a background job.
- **Scope control.** I cut things the rules do not need: no sweeper, no rate limiter, no login, no seed data. Less code means fewer bugs.
- **Test design.** I decided which tests must exist and what each must prove. I asked for tests that attack the weakest moment: payment in the exact second a hold ends.
- **Review design.** I ran several independent plan reviews with one strict prompt. I merged the answers. I picked a side when they disagreed. I wrote the reason in `ASSUMPTIONS.md`.
- **User focus.** The users are guardians on phones. So the app is Bangla first, with an English switch. I tested it at 360 px.
- **Deploy care.** The API address is read when the container starts. The URL can change without a rebuild.

---

## How I worked

**1. Requirements first.** I wrote what I wanted. Every rule covered. Bangla and English. Easy on a phone. Clear file and table names. A design that does not look like a template. I also fixed the scope: only what the assignment asks.

**2. A written brief before any code.** I made the brief with an AI assistant. It had the API contract field by field. It had an error code for every failure. It had the seat rule, the time rule, the test list, the screens and the wording in both languages.

**3. Several independent reviewers for the plan.** I gave the assignment and the brief to several AI assistants, one by one. The prompt was strict: find what is missing, find a race, find what a hidden test would break, say what to cut. This found seven mistakes before any code existed.

**4. Small steps.** Claude Code built one part at a time. One commit for each part.

**5. A second reviewer for the code.** Before the first run, a different AI reviewed the code. I gave it exact questions. Can seats ever be oversold? Can any bad input return a 500? Do the tests test what their names say? It found three real bugs.

**6. Prove it.** I built and tested the backend in Docker on PostgreSQL 17. I asked for harder tests. A crowd applying on two backends at once. Payments sent in the exact second a hold ends. After every run, the tests ask the database if any class has more seats in use than it has.

**7. Use it.** I opened the app in a browser. Phone size and desktop size. Both languages. I sent back what looked wrong.

---

## What I gave to the AI, and what I kept

| Gave to the AI | Kept for myself |
|---|---|
| Controllers, DTOs, SQL boilerplate | The locking plan and the transaction design |
| Test scaffolding | Which tests must exist, and what each must prove |
| First drafts of the README and other documents | Every status code where the assignment was silent |
| CSS and layout | What to cut |

---

## Decisions, and why

The AI proposed most of these. I chose them after the reviews. I can explain each one.

| Decision | Why |
|---|---|
| Lock the class row (`select ... for update`) in one transaction | Two requests cannot both see "one seat left". The lock lives in PostgreSQL, so two backends are safe. |
| Count seats from holds. No counter. | A count is right the moment a hold expires. A counter needs a background job. |
| `clock_timestamp()` for every time check | `now()` is fixed at the start of a transaction. It is stale after a lock wait. One clock means no disagreement. |
| Dapper with plain SQL, not a full ORM | Each seat rule is one SQL statement. I can read it and explain it. I know SQL well. I was new to C#. |
| Partial unique index on active holds | A second safety net under "one active application per class". |
| No background sweeper, no rate limiter | The rules do not need them. Extra code is extra risk. |
| Class names are not unique | A test suite that makes the same class each run would fail on run two. |
| Bangla first, English switch | The assignment gives its button labels in Bangla. The users are guardians. |
| API address read at container start | Vite fixes variables at build time. A runtime file lets the URL change with no rebuild. |

---

## Where the AI was wrong, and how I caught it

### In the plan, before any code

| # | What was wrong | Caught by |
|---|---|---|
| 1 | Confirm and cancel used `now()`. Apply used `clock_timestamp()`. A confirm that began just before expiry could pass after expiry. | Plan review, and my "one clock" rule |
| 2 | The seat count inside the lock ignored expiry. A class could say "full" when a seat had just become free. | Plan review |
| 3 | nginx had no fallback to `index.html`. A refresh on an application would give a 404. This breaks "refresh keeps your application". | Plan review |
| 4 | The compose file did not pass `HOLD_DURATION_SECONDS`. Nobody could change the hold time. | Plan review |
| 5 | Endpoints with no body would reject a request with no `Content-Type`. | Plan review |
| 6 | Ids were typed as `Guid` in routes. The framework would answer a bad id with its own body. | Plan review |
| 7 | Error responses would lose their CORS headers. The browser would show "class full" as a network error. | Plan review |

### In the code

| # | What was wrong | Caught by | Fix |
|---|---|---|---|
| 8 | A quick "class is full" check ran before the student's own hold was checked. A student holding the last seat got CLASS_FULL. ALREADY_APPLIED was right. | Claude Code, while writing tests | The locked transaction answers now. A test covers it. |
| 9 | A body of just `null` would have given a 500 on `POST /classes`. The assignment says bad input is never a 5xx. | The bad-input test | `null` is an empty form, so 400. |
| 10 | The countdown used the phone's clock for its first second. | A failing frontend test | A new server time redraws every countdown. |
| 11 | The connection pool was never closed. Each test host left 40 connections open. | The second AI's code review | The container owns the pool and closes it. |
| 12 | Server time was cut to whole seconds. The frontend replaced its clock estimate on every answer. An expired hold could show "on hold" again for a moment. | The second AI's code review | Milliseconds on the server. The client keeps its best estimate. |
| 13 | Two rare paths in confirm and cancel could give a 500. | The second AI's code review | Retry once, then 409. |
| 14 | **Paying did not take the class lock.** A payment in the exact second a hold ended could race with a new applicant. One seat could go to two students. Every normal test passed. | Claude Code, after I asked for a test aimed at that exact second | Paying takes the same lock. A stress test checks the database after every round. |
| 15 | The AI added class name rules the assignment never asks for. A valid name from an automated test could fail. | The second AI's review. I approved the removal. | Removed. |
| 16 | On a phone, the sticky header covered half the screen. The 17 digit number did not fit. | Browser check at 360 px | The header scrolls away on phones. |
| 17 | The first design looked generic. | My own review | New palette, dark header, dark mode. |
| 18 | The first README draft described the project wrongly. | A re-read against the brief | Fixed before the first commit. |

---

## What I learned

- **The dangerous bugs were about time and locking.** Items 1, 2, 12 and 14 are the same kind of mistake. Next time I start with the failure I fear most. I write the expiry-second stress test first.
- **A green test is only as good as its question.** The payment race passed everything until I asked for a test aimed at it.
- **Reviewers disagree. That helps.** The reviewers gave different answers on three status codes. I had to choose. That made me understand the trade-off.
- **Check the database, not only the API.** My stress tests end by asking PostgreSQL.
- **Keep the reviewer independent.** Give it the spec and the code. Not the author's story.

---

## Limits

The AI wrote most of the lines, including all the C#.

I can explain the design, the SQL, every test and why each rule holds.

I am still learning the finer points of ASP.NET Core. I am slower with framework internals. I say so when asked.
