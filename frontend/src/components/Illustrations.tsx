// One family of drawings: graphite outline at one stroke width, round caps, and pencil
// yellow as the only fill. Used only where a screen is empty or a state changes, never
// as background. Each is decorative; the words next to it carry the meaning.

const line = { fill: "none", stroke: "var(--ink)", strokeWidth: 3, strokeLinecap: "round", strokeLinejoin: "round" } as const;
const filled = { fill: "var(--pencil)", stroke: "var(--ink)", strokeWidth: 3, strokeLinejoin: "round" } as const;

function Frame({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <svg className={`illustration ${className ?? ""}`} viewBox="0 0 160 120" aria-hidden="true" focusable="false">
      {children}
    </svg>
  );
}

// Admission not open yet: a school gate, shut, with a padlock.
export function ClosedGate() {
  return (
    <Frame>
      <path {...line} d="M20 108V40M140 108V40M14 40h18M128 40h18M10 108h140" />
      <path {...line} d="M32 52h48M32 104h48M80 52h48M80 104h48M44 52v52M56 52v52M68 52v52M92 52v52M104 52v52M116 52v52" />
      <path {...line} d="M74 70v-6a6 6 0 0 1 12 0v6" />
      <rect {...filled} x="70" y="70" width="20" height="16" rx="2" />
    </Frame>
  );
}

// Class full, with a waiting list: a row of benches, every seat taken.
export function FullBenches() {
  return (
    <Frame>
      {[20, 64, 108].map((x) => (
        <g key={x}>
          <path {...line} d={`M${x} 50v28M${x + 6} 78v26M${x + 30} 78v26M${x} 60h14`} />
          <rect {...filled} x={x} y="72" width="36" height="8" rx="2" />
        </g>
      ))}
      <path {...line} d="M10 108h140" />
    </Frame>
  );
}

// No applications yet: an open register with empty ruled lines.
export function EmptyRegister() {
  return (
    <Frame>
      <path {...line} d="M80 30c-16-8-36-8-56-2v74c20-6 40-6 56 2c16-8 36-8 56-2V28c-20-6-40-6-56 2zM80 30v74" />
      <path {...line} d="M36 48h32M36 62h32M36 76h32M92 48h32M92 62h32M92 76h20" />
      <rect {...filled} x="112" y="14" width="10" height="34" rx="2" transform="rotate(20 117 31)" />
    </Frame>
  );
}

// Payment confirmed: the slip with the round stamp pressed on it.
export function StampedSlip() {
  return (
    <Frame>
      <path {...line} d="M36 16h88v88H36zM50 36h44M50 50h60M50 64h30" />
      <circle {...filled} cx="104" cy="82" r="18" />
      <path {...line} d="M96 82l6 6l11 -12" />
    </Frame>
  );
}

// A seat offered from the waiting list: a slip rising out of the queue.
export function OfferRising() {
  return (
    <Frame>
      <path {...line} d="M20 100h120M34 100v-12M56 100v-12M104 100v-12M126 100v-12" />
      <rect {...filled} x="58" y="26" width="44" height="54" rx="3" />
      <path {...line} d="M68 42h24M68 54h16M80 92V84M72 90l8 8l8 -8" />
    </Frame>
  );
}

// Page not found: a register page torn out, with nothing written on it.
export function TornPage() {
  return (
    <Frame>
      <path {...line} d="M44 14h72v84l-10 -6l-10 8l-10 -8l-10 8l-10 -8l-10 8l-12 -6z" />
      <path {...line} d="M58 38h44M58 52h44M58 66h24" />
      <circle {...filled} cx="118" cy="104" r="8" />
    </Frame>
  );
}

// Server unreachable: a telephone handset off the hook, the cord loose.
export function LooseCord() {
  return (
    <Frame>
      <path {...line} d="M30 70c10-24 30-30 50-30s40 6 50 30M30 70l14 6l6 -14M130 70l-14 6l-6 -14" />
      <path {...line} d="M80 60c0 14 -10 16 -10 26s20 8 20 18" />
      <rect {...filled} x="62" y="92" width="36" height="16" rx="3" />
    </Frame>
  );
}
