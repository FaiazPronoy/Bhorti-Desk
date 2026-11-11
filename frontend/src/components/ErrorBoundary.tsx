import { Component, type ErrorInfo, type ReactNode } from "react";

interface ErrorBoundaryProps {
  message: string;
  reloadLabel: string;
  children: ReactNode;
}

// A bug in one page shows a short message instead of a blank screen.
export class ErrorBoundary extends Component<ErrorBoundaryProps, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("Page crashed", error, info.componentStack);
  }

  render() {
    if (!this.state.failed) return this.props.children;
    return (
      <div className="sheet empty-state" role="alert">
        <p>{this.props.message}</p>
        <button type="button" className="button button--primary" onClick={() => window.location.reload()}>
          <span>{this.props.reloadLabel}</span>
        </button>
      </div>
    );
  }
}
