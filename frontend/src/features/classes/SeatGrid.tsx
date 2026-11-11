import type { ClassSeatSummary } from "../../api/apiTypes";

export const SEAT_GRID_LIMIT = 200;

// One small square per seat, ten to a row like benches. Filled is confirmed, hatched
// is on hold, outlined is free. Only for classes of up to 200 seats.
export function SeatGrid({ seats }: { seats: ClassSeatSummary }) {
  const squares = Array.from({ length: seats.totalSeats }, (_, index) => {
    if (index < seats.confirmedSeats) return "confirmed";
    if (index < seats.confirmedSeats + seats.heldSeats) return "held";
    return "free";
  });
  return (
    <div className="seat-grid" aria-hidden="true">
      {squares.map((kind, index) => (
        <span key={index} className={`seat seat--${kind}`} />
      ))}
    </div>
  );
}
