import React from "react";

/**
 * The Endatix CSS sheep, as used on endatix.com/404 and the Hub error pages
 * (`hub/components/error-handling/sheep-buddy.tsx`). Keep the three in step:
 * the markup is the contract, all of the drawing lives in `endatix-theme.css`
 * under `.edx-sheep`.
 *
 * Decorative only — it carries no meaning the surrounding copy does not.
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
