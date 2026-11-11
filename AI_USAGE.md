# AI usage

## Tools

- **Claude Code** (Anthropic), running the Claude Opus 5.5 model, wrote the code, the tests and the first drafts of the documents.
- Other AI assistants helped Faiaz plan the work and review the plan before any code was written.

## How it was used

Faiaz read the assignment, decided the requirements, the stack and the design, and wrote them into a detailed build brief: the API contract, the error codes, the locking approach, the tests that must exist, the screens and the wording in both languages. Claude Code built the project from that brief. Faiaz reviewed the work, ran the app and the tests with Docker, and asked for fixes.

## What Faiaz decided or checked himself

- The stack: .NET with Dapper and hand-written SQL, PostgreSQL, React with TypeScript.
- The seat rule: a row lock on the class inside one transaction, seats counted from holds rather than stored, and the database clock for every time check.
- The status codes and the order of checks (404, then 403, then status).
- The Bangla wording, the "admission register" look, and that the payment is clearly marked as a demo.
- That no login, seed data or rate limiter is added, as the assignment requires.

## Where the AI was wrong and how it was caught

- **A quick "class is full" check ran before the student's own hold was looked at.** A student who already held the last seat and applied again got CLASS_FULL instead of ALREADY_APPLIED. Found while writing the backend tests, by checking the code against the planned order of checks. The check now lets the locked transaction answer when the student already has a seat, and a test uses a one seat class to cover it.
- **A request body of just `null` would have caused a 500 on POST /classes.** Found while writing the bad input test. A null body is now treated as an empty form and gets a 400 with field errors.
- **The countdown used the device clock for its first second.** On a device whose clock was behind the server it briefly showed the wrong time. A frontend test caught it. A new server time now redraws every countdown at once.
- **The frontend packages were first installed with a Node version that was too old,** so the test runner could not start. They were reinstalled with Node 22, which the Docker image also uses.
- **On a 360 px phone the sticky header covered almost half the screen** and the 17 digit number did not fit in its box. Seen in a browser check. The header now scrolls away on phones and the box uses a smaller font there.
- **The first README draft named the wrong origin for the project,** taken from Faiaz's other repositories instead of this project's brief. Caught on a re-read before committing.

Faiaz then had a second AI assistant review the code before the first run. It found three more problems, all fixed before the tests ran:

- **The database connection pool was never closed** when the app stopped, so each test host left its connections open.
- **The server time was sent in whole seconds and the frontend replaced its clock estimate on every answer,** so an expired hold could briefly show as on hold again. The server time now has milliseconds and the frontend keeps its best estimate.
- **Two rare code paths in confirm and cancel could answer 500.** They now try once more and then answer 409 HOLD_EXPIRED.

While writing harder stress tests, Claude Code found one more problem in its own code:

- **Paying in the exact second a hold ended could race with a new applicant.** Paying did not take the class lock that applying takes, so in a tiny window both could succeed and one seat would go to two students. Paying now takes the same lock. A test keeps paying and applying across the expiry second and then checks the database directly.
