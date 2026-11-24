// Draws the QR modules the server sends as plain SVG squares. One path, so it prints
// sharp at any size, and no markup from the server is put into the page.
export function QrCode({ modules, label }: { modules: boolean[][]; label: string }) {
  const size = modules.length;
  const quiet = 4;
  let path = "";
  modules.forEach((row, y) =>
    row.forEach((dark, x) => {
      if (dark) path += `M${x + quiet} ${y + quiet}h1v1h-1z`;
    }),
  );
  return (
    <svg className="qr" viewBox={`0 0 ${size + quiet * 2} ${size + quiet * 2}`} role="img" aria-label={label} shapeRendering="crispEdges">
      <rect width="100%" height="100%" fill="#ffffff" />
      <path d={path} fill="#000000" />
    </svg>
  );
}
