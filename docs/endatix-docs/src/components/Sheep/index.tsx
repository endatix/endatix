import React from "react";

/**
 * Decorative CSS sheep for the docs 404. Markup must stay aligned with Hub
 * `SheepBuddy` and the corp 404; styles live under `.edx-sheep` in
 * `endatix-theme.css`.
 */
export default function Sheep(): React.ReactNode {
  return (
    <div className="edx-sheep" aria-hidden="true">
      <div className="top">
        <div className="body" />
        <div className="head">
          <div className="eye one" />
          <div className="eye two" />
          <div className="ear one" />
          <div className="ear two" />
        </div>
      </div>
      <div className="legs">
        <div className="leg" />
        <div className="leg" />
        <div className="leg" />
        <div className="leg" />
      </div>
    </div>
  );
}
