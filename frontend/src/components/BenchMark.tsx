// The Bhorti Desk mark: a school bench seen from the side, with its seat filled in
// pencil yellow. One drawing at every size, from the 16 px favicon to the banner.
export function BenchMark({ size = 28 }: { size?: number }) {
  return (
    <svg viewBox="0 0 32 32" width={size} height={size} aria-hidden="true" focusable="false">
      <path d="M6 5v13" fill="none" stroke="var(--ink)" strokeWidth="2.5" strokeLinecap="round" />
      <rect x="6" y="15" width="21" height="5" rx="1" fill="var(--pencil)" stroke="var(--ink)" strokeWidth="2.5" strokeLinejoin="round" />
      <path d="M10 20v8M23 20v8M6 9h8" fill="none" stroke="var(--ink)" strokeWidth="2.5" strokeLinecap="round" />
    </svg>
  );
}
