// Shapes of the API answers, exactly as the backend sends them.

export type HoldStatus = "HELD" | "CONFIRMED" | "EXPIRED" | "CANCELLED";

export interface AdmissionClass {
  id: string;
  name: string;
  totalSeats: number;
}

export interface ClassSeatSummary extends AdmissionClass {
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
}

export interface CreatedHold {
  holdId: string;
  classId: string;
  studentId: string;
  status: HoldStatus;
  expiresAt: string;
  className?: string;
  serverTime?: string;
}

export interface HoldDetails {
  holdId: string;
  classId: string;
  status: HoldStatus;
  expiresAt: string;
  className?: string;
  createdAt?: string;
  serverTime?: string;
}

export interface ConfirmedHold {
  holdId: string;
  status: "CONFIRMED";
}

export interface StudentHold {
  holdId: string;
  classId: string;
  className: string;
  status: HoldStatus;
  expiresAt: string;
  createdAt?: string;
  serverTime?: string;
}

export interface ApiErrorBody {
  code: string;
  message: string;
  fieldErrors?: Record<string, string>;
  holdId?: string;
}
