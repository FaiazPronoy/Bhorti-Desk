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
  // Human serial such as BD-2026-0001234. The hold id stays internal.
  serial?: string;
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
  serial?: string;
}

export interface ApiErrorBody {
  code: string;
  message: string;
  fieldErrors?: Record<string, string>;
  holdId?: string;
}

export interface Me {
  kind: "GUARDIAN" | "STAFF";
  id: string;
  role: "ADMIN" | "OFFICER" | null;
}

export type SessionWindow = "UPCOMING" | "OPEN" | "ENDED";

export interface AdmissionSession {
  id: string;
  academicYear: number;
  name: string;
  opensAt: string;
  closesAt: string;
  status: "DRAFT" | "PUBLISHED" | "CLOSED";
  window: SessionWindow;
  classIds: string[];
}

export interface CurrentSeason {
  session: AdmissionSession | null;
  serverTime: string;
}

export interface SessionForm {
  academicYear: number | null;
  name: string;
  opensAt: string | null;
  closesAt: string | null;
}

export interface ClassRules {
  classId: string;
  minAge: number | null;
  maxAge: number | null;
  ageCutoffDate: string | null;
  feeTaka: number;
}

export type ClassRulesForm = Omit<ClassRules, "classId">;

export interface Applicant {
  studentNameBn: string;
  studentNameEn: string;
  dateOfBirth: string;
  guardianName: string;
  guardianPhone: string;
  previousSchool: string | null;
}

// An empty date goes to the API as null, so it gets its own clear field error.
export type ApplicantInput = Omit<Applicant, "dateOfBirth"> & { dateOfBirth: string | null };

export interface ApplicationDetails {
  holdId: string;
  serial: string;
  classId: string;
  className: string;
  status: HoldStatus;
  expiresAt: string;
  serverTime: string;
  rules: ClassRules | null;
  applicant: Applicant | null;
  documents: DocumentInfo[];
  payment: PaymentInfo | null;
  review: ReviewInfo | null;
}

export interface PaymentInfo {
  paymentId: string;
  gateway: "SANDBOX";
  status: "PENDING" | "PAID" | "FAILED" | "LATE";
  amountTaka: number;
  finishedAt: string | null;
}

export interface ReviewInfo {
  decision: "VERIFIED" | "REJECTED";
  reason: string | null;
  refundDue: boolean;
}

export interface PaymentStart {
  paymentId: string;
  amountTaka: number;
  gateway: string;
  status: string;
  hostedPageUrl: string;
}

export interface SandboxPage {
  paymentId: string;
  holdId: string;
  className: string;
  serial: string;
  amountTaka: number;
  status: PaymentInfo["status"];
}

export interface WaitlistPlace {
  entryId: number;
  classId: string;
  className: string;
  status: "WAITING" | "OFFERED";
  position: number;
  offeredHoldId: string | null;
}

export type NoticeKind = "HELD" | "TWO_MINUTES_LEFT" | "PAID" | "VERIFIED" | "REJECTED" | "EXPIRED" | "OFFERED";

export interface Notice {
  id: number;
  holdId: string;
  className: string;
  kind: NoticeKind;
  createdAt: string;
}

export type AdmissionStatus = "PAID" | "VERIFIED" | "REJECTED";

export interface Slip {
  holdId: string;
  serial: string;
  className: string;
  maskedStudentId: string;
  admissionStatus: AdmissionStatus;
  studentNameBn: string | null;
  studentNameEn: string | null;
  hasPhoto: boolean;
  verifyCode: string;
  qr: boolean[][];
}

export interface SlipCheck {
  className: string;
  maskedStudentId: string;
  admissionStatus: AdmissionStatus;
}

export interface OfficeApplication {
  holdId: string;
  serial: string;
  classId: string;
  className: string;
  status: HoldStatus;
  studentIdLastFour: string;
  applicant: Applicant | null;
  documents: DocumentInfo[];
  payment: PaymentInfo | null;
  review: ReviewInfo | null;
}

export type DocumentKind = "PHOTO" | "CERTIFICATE";

export interface DocumentInfo {
  kind: DocumentKind;
  contentType: string;
  sizeBytes: number;
}

export type Gender = "MALE" | "FEMALE" | "OTHER";

export interface Student {
  id: string;
  birthRegistrationNumber: string;
  fullName: string;
  dateOfBirth: string;
  gender: Gender;
}

export interface NewStudent {
  birthRegistrationNumber: string;
  fullName: string;
  dateOfBirth: string;
  gender: Gender | "";
}
