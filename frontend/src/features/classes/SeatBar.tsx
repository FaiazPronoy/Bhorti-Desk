import type { ClassSeatSummary } from "../../api/apiTypes";

// Phones, and classes too big for the grid: one bar in three parts. It is a picture
// only; the numbers are always printed next to it.
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
