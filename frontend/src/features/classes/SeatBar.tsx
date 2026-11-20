import type { ClassSeatSummary } from "../../api/apiTypes";

// One bar in three parts for every class, whatever its size, so every row of the
// register has the same height. It is a picture only; the numbers sit beside it.
export function SeatBar({ seats }: { seats: ClassSeatSummary }) {
  const total = Math.max(seats.totalSeats, 1);
  const confirmed = (seats.confirmedSeats / total) * 100;
  const held = (seats.heldSeats / total) * 100;
  return (
    <div className="seat-bar" aria-hidden="true">
      <span className="seat-bar__confirmed" style={{ width: `${confirmed}%` }} />
      <span className="seat-bar__held" style={{ width: `${held}%` }} />
    </div>
  );
}
