# StockPulse Premium Fintech UI Design

**Date:** 2026-07-29  
**Scope:** Redesign the existing Angular dashboard only. Preserve the current REST, SignalR, watchlist, and virtual-scrolling behavior.  
**Visual direction:** Premium Fintech Workspace — dark, calm, high-signal, and data-oriented.

## Goals

- Make the real-time news workflow quicker to scan and more polished.
- Keep status, sentiment, impact, tickers, and time understandable at a glance.
- Make the desktop inspector useful and preserve watchlist access on mobile.
- Improve accessibility, responsive behavior, and interaction feedback without adding dependencies.

## Information Architecture

### Desktop

The dashboard remains a three-column workspace:

1. **Watchlist sidebar** — product identity, watchlist count, and the existing add/remove ticker actions.
2. **Live news feed** — primary workspace with a compact utility header, realtime status, article cards, and virtual scrolling.
3. **Article inspector** — selected article context, source, timestamp, ticker chips, sentiment, impact, summary, and a safe external-link action. Until a card is selected, it gives a concise orientation prompt.

Selecting a news card updates the inspector. Opening its source remains an explicit external-link action, so selection never unexpectedly navigates away.

### Narrow screens

- The news feed remains the primary content.
- The watchlist is reachable through a compact, accessible disclosure instead of being hidden.
- The inspector opens in the normal document flow below the selected card/feed; it is not a nested scrolling area.

## Visual System

- **Surfaces:** navy-black page background; elevated, subtly bordered panels; no decorative gradients or glass effects.
- **Color semantics:** blue for interactive focus and actions, green for positive/live, amber for caution or neutral, and red for negative/error. Color never carries meaning alone; labels remain visible.
- **Typography:** system sans-serif for reading; the existing monospace stack only for tickers, scores, and timestamps. Use a 12/14/16/20/28 scale and tabular numerals for stable data alignment.
- **Spacing and shape:** 4/8px spacing rhythm, 12px card radius, and a small fixed elevation scale.
- **Motion:** 160–220ms opacity/transform transitions only. New-news emphasis fades once; all non-essential movement is disabled under `prefers-reduced-motion`.

## Component Changes

### Dashboard shell

- Add a compact product/utility header on desktop and a mobile control for the watchlist.
- Maintain the current 3-column arrangement at large widths, collapse predictably at tablet sizes, and avoid horizontal overflow.

### Watchlist panel

- Promote the add-ticker form to a clearly labelled primary action with an accessible 44px button target.
- Present each ticker as a scannable row with consistent metadata and a labelled remove action.
- Keep validation and loading/error behavior unchanged, but align their visual states with the semantic tokens.

### News feed and cards

- Display the live status as icon plus text, with a clear offline/reconnecting state.
- Make each card fully selectable by keyboard, with a visible selected state and focus ring.
- Strengthen hierarchy: source and time first, readable headline, then summary and semantic metadata chips.
- Retain fixed virtual-row height by clamping the summary, so scrolling performance remains unchanged.

### Article inspector

- Replace the static placeholder with a selected-news view driven from the existing `NewsItem` data.
- Use the source URL only in an explicit, labelled external link with `target="_blank"` and `rel="noopener noreferrer"`.
- Provide an accessible empty state when nothing is selected.

## Accessibility and Quality Requirements

- Normal text and focus indicators meet WCAG AA contrast requirements.
- Interactive controls have visible keyboard focus and at least 44px touch targets where practical.
- Status, sentiment, errors, and connection state include text labels; screen-reader labels remain descriptive.
- No emoji or new icon library is introduced; lightweight CSS/SVG treatment uses the existing dependency set.
- Preserve `OnPush`, `trackBy`, batched SignalR updates, and CDK virtual scrolling. Do not add API calls or change backend contracts.

## Verification

- Run Angular unit tests and production build.
- Check the dashboard at 375px, 768px, 1024px, and 1440px widths.
- Verify keyboard selection, opening the external article link, watchlist add/remove, loading/empty/error states, connection labels, and reduced-motion behavior.

## Out of Scope

- New market-price data, charts, filters, authentication, backend endpoints, or changes to the news/watchlist API.
- Light theme and new third-party UI dependencies.
