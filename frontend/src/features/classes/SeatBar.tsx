import type { ClassSeatSummary } from "../../api/apiTypes";

// One bar in two parts for every class, whatever its size, so every row of the register
// has the same height. It is a picture only; the numbers sit beside it. The parts are
// scaled, not resized, so a seat filling in slides smoothly.
export function SeatBar({ seats }: { seats: ClassSeatSummary }) {
  const total = Math.max(seats.totalSeats, 1);
  const confirmed = Math.min(1, seats.confirmedSeats / total);
  const held = Math.min(1 - confirmed, seats.heldSeats / total);
  return (
    <div className="seat-bar" aria-hidden="true">
      <span className="seat-bar__held" style={{ transform: `translateX(${confirmed * 100}%) scaleX(${held})` }} />
      <span className="seat-bar__confirmed" style={{ transform: `scaleX(${confirmed})` }} />
    </div>
  );
}
