# Assumptions and decisions

1. **Seats are counted, not stored.** Taken seats are the holds that are CONFIRMED, or HELD with time left, counted at read time. An expired hold stops counting the moment its time passes, so no background job is needed. A row may still say HELD in the table after it expired; every query treats it as EXPIRED.
2. **How the seat limit is kept.** Applying runs one transaction that locks the class row with `select ... for update`, counts the taken seats and inserts the hold. Every request for that class waits at the database in turn, so two requests can never both take the last seat. Paying takes the same class lock, so a payment in the very second a hold ends cannot race with a new applicant. The lock lives in PostgreSQL, not in the API process, so the same is true for two backend processes on one database. All time checks use the database clock.
3. **One hold per class per student.** A student can hold seats in different classes at once, because the rule speaks of one class. A partial unique index backs this up in the database.
4. **Applying again is allowed** after a hold EXPIRED or was CANCELLED.
5. **A confirmed admission cannot be cancelled** through this API. Cancel on it returns 409 HOLD_ALREADY_CONFIRMED.
6. **Repeats are safe.** A second confirm by the owner returns 200. A second cancel returns 204. Cancel after expiry returns 409 HOLD_EXPIRED.
7. **Status codes.** 400 bad input or bad header, 403 another student's hold, 404 unknown class or hold, 409 for rule conflicts (full, already applied, expired, cancelled, confirmed), 503 when the database stays busy. Checks run in the order 404, then 403, then status.
8. **X-Student-Id** must be exactly 17 ASCII digits after trimming spaces, as the assignment's example shows. Anything else is 400. The frontend turns Bangla digits into ASCII before sending.
9. **Class names** are not unique. They need 2 to 100 characters and at least one letter, and extra spaces are squeezed. `totalSeats` is a whole number from 1 to 100000.
10. **GET /holds/{holdId} needs no header**, as the assignment shows none, so it never returns the student number.
11. **expiresAt is in whole seconds** (`2026-11-01T04:10:00Z`) to match the assignment's example, so a hold can be up to one second shorter than the setting.
12. **Classes cannot be edited or deleted.** The assignment has no endpoint for it.
13. **No seed data.** A fresh database is empty.
14. **No authentication**, as the assignment requires. Anyone who knows a birth registration number can act as that student, and anyone can create a class. A real deployment needs guardian sign-in, an admin login for the school office, and rate limiting.
15. **Dapper with hand-written SQL** is the ORM. Every seat rule is one SQL statement, so writing the SQL directly is the clearest way to show and check it, and there is no migration tooling to get wrong.
16. **The UI remembers the number on the device** (localStorage), so a guardian who closes the browser comes back to the running hold. The "নম্বর বদলান" (Change number) button clears it.
17. **Payment is simulated.** "ভর্তি ফি দিন" confirms the hold. No money moves, and no fee amount is stored because the assignment gives none.
18. **Language and time.** The UI starts in Bangla with an English switch. The API sends UTC; the screen shows local time.
19. **Busy database.** If waiting for the class lock passes 20 seconds twice in a row, the answer is 503 SERVER_BUSY with Retry-After. This should not happen in normal use.
