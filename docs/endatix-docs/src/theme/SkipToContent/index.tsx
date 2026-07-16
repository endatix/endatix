import React from 'react';
import OriginalSkipToContent from '@theme-original/SkipToContent';

// Wrapping with data-nosnippet prevents Google from picking up
// "Skip to main content" as a sitelink label or snippet text.
export default function SkipToContent(): React.JSX.Element {
  return (
    <div data-nosnippet="">
      <OriginalSkipToContent />
    </div>
  );
}
