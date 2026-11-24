// Hundreds of guardians reach for the last seats of one class at the same moment,
// through nginx in front of two backends. Every guardian presses Apply twice, as people
// do on a slow phone. Afterwards, check-seats.sql proves the seat rule from the database.
import http from "k6/http";
import { check } from "k6";
import { Counter } from "k6/metrics";

const API = __ENV.API || "http://localhost:8080";
const GUARDIANS = 600;
const SEATS = 50;
const ALREADY_TAKEN = 40;

const created = new Counter("seats_taken");
const full = new Counter("class_full_answers");
const serverErrors = new Counter("server_errors");

export const options = {
  scenarios: {
    rush: { executor: "per-vu-iterations", vus: GUARDIANS, iterations: 1, maxDuration: "2m" },
  },
  thresholds: {
    server_errors: ["count==0"],
    http_req_failed: ["rate<1"],
  },
};

function studentId(n) {
  return "2019" + String(1000000000000 + n).slice(-13);
}

export function setup() {
  const res = http.post(`${API}/classes`, JSON.stringify({ name: "Load test class", totalSeats: SEATS }), {
    headers: { "Content-Type": "application/json" },
  });
  const classId = res.json("id");
  // Fill most of the class first, so the rush is for the last ten seats.
  for (let i = 0; i < ALREADY_TAKEN; i++) {
    http.post(`${API}/classes/${classId}/holds`, null, { headers: { "X-Student-Id": studentId(900000 + i) } });
  }
  return { classId };
}

export default function (data) {
  const headers = { headers: { "X-Student-Id": studentId(__VU) } };
  const first = http.post(`${API}/classes/${data.classId}/holds`, null, headers);
  const second = http.post(`${API}/classes/${data.classId}/holds`, null, headers);
  for (const res of [first, second]) {
    if (res.status === 201) created.add(1);
    if (res.status === 409 && res.json("code") === "CLASS_FULL") full.add(1);
    if (res.status >= 500) {
      serverErrors.add(1);
      console.error(`server error ${res.status}: ${res.body}`);
    }
    check(res, { "answered without a server error": (r) => r.status < 500 });
  }
}

export function teardown(data) {
  const seats = http.get(`${API}/classes/${data.classId}`).json();
  console.log(`class after the rush: total ${seats.totalSeats}, held ${seats.heldSeats}, confirmed ${seats.confirmedSeats}, free ${seats.availableSeats}`);
}
