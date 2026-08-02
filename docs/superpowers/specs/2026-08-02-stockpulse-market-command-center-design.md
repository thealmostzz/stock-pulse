# StockPulse Market Command Center UI Design

**Date:** 2026-08-02  
**Scope:** Redesign the Angular dashboard's information hierarchy and presentation. Preserve the existing news/watchlist REST APIs, SignalR contract, filtering, sorting, pagination, and virtual scrolling.  
**Direction:** Dark Market Command Center — a modern SaaS workspace for scanning live market news and acting on a selected article.

## Goals

- Make the visual structure substantially different from the existing three-panel screen.
- Put system status, filters, and the current news stream in a clear reading order.
- Make selected-news context feel like a deliberate analysis workspace rather than a placeholder side column.
- Keep the initial bundle, realtime behavior, and interaction accessibility intact.

## Desktop Information Architecture

### 1. Top app bar

A persistent top app bar spans the application rather than living inside the news feed. It contains:

- StockPulse product mark and the active workspace label.
- Realtime status with text and a semantic status dot.
- A concise watchlist count.
- An explicit primary action for adding a ticker, which focuses/opens the existing watchlist form rather than creating a new API flow.

The bar remains visible on scroll without covering content. It reserves its own layout space and has a visible keyboard focus path.

### 2. Left navigation rail

The current watchlist becomes a compact SaaS rail. The product/navigation area is visually separate from the ticker-management area. The existing watchlist, add-ticker form, validation, and remove action stay functionally unchanged, but ticker rows are designed as compact selectable chips/rows rather than a plain vertical list.

### 3. Main news workspace

The primary workspace begins with a page header containing the live-news title, visible total count, and the existing filter/sort controls. A small status strip directly below summarizes the current view (live connection state, active filter count, and shown/total items) without inventing unavailable market-price data.

News cards become elevated, grouped cards with stronger hierarchy:

- source/time and signals form a quiet metadata row;
- headline is the strongest element;
- ticker and impact metadata become a consistent footer;
- selected state uses the accent color, never the positive-sentiment color;
- keyboard activation and 154px virtual row size remain unchanged.

Cards retain a single-column virtualized stream; this avoids incompatible grid virtualization and preserves current scrolling performance. The visual change comes from elevation, grouping, gutters, and card rhythm rather than changing the data structure.

### 4. Insight panel

The inspector is a prominent dedicated analysis panel with a labelled header, selected article title, metadata, ticker chips, sentiment/impact summary, and an explicit external-link action. Its empty state explains how to select an article. It is visually separated with a raised surface and does not perform a detail API request.

## Responsive Behavior

- **≥ 1200px:** rail, workspace, and insight panel are visible; the top app bar is persistent.
- **768–1199px:** rail remains available; insight panel moves below the workspace in the normal document flow.
- **< 768px:** top app bar shows a watchlist toggle. The watchlist is a native disclosure/drawer-like region, while the workspace is first in reading order. The inspector follows the feed after a card is selected.
- No breakpoint may hide the only control that reopens the watchlist. Viewport changes must preserve an accessible route back to it.

## Visual System

- **Theme:** dark OLED/navy surfaces with slate borders; no light-theme implementation in this scope.
- **Semantic colors:** blue accent for selection/action/focus; green, amber, and red only for positive, neutral/caution, and negative/error signals.
- **Typography:** system sans-serif for UI/prose; semantic monospace only for tickers, scores, counts, and timestamps.
- **Spacing:** 4/8px scale, 12px card radii, consistent surface elevation, and responsive gutters.
- **Motion:** 160–220ms opacity/transform/color transitions; no layout-property animation; suppress non-essential motion with `prefers-reduced-motion`.

## Component Boundaries

- `DashboardComponent` owns layout-only state such as mobile watchlist visibility and continues to own the selected `NewsItem`.
- `NewsFeedComponent` owns feed header/status presentation and emits article selection exactly as it does today.
- `NewsCardComponent` remains the only interactive unit for choosing a news item.
- `NewsInspectorComponent` remains read-only and receives `NewsItem | null`.
- `WatchlistPanelComponent` retains API/validation ownership; any top-bar action delegates to its existing form through a focused public method or an accessible native control, without duplicating ticker mutation logic.

## Accessibility and Performance Requirements

- Normal text/focus colors meet WCAG AA contrast; color is never the sole status indicator.
- Every icon-only control has an accessible name and at least a 44px target.
- Keyboard tab order follows app bar → rail → workspace → insight panel. A skip link reaches the main workspace.
- Keep Angular `OnPush`, `trackBy`, CDK virtual scrolling, batched SignalR updates, pagination behavior, and the 300-item memory cap.
- No new third-party dependencies, web fonts, backend endpoints, or market-price/chart data.

## Verification

- Add/update Jasmine specs for top-bar labelling, mobile watchlist access, selected-card state, inspector handoff, and existing watchlist add/remove behavior.
- Run frontend unit tests and production build.
- Manually inspect 375px, 768px, 1024px, and 1440px; test keyboard navigation, reduced motion, filter/sort, realtime connection labels, card selection, and the external article link.

## Out of Scope

- New portfolio, pricing, chart, alert, authentication, backend, or API capabilities.
- Light mode, third-party component libraries, and replacement of the existing virtual-scroll feed with a CSS grid.
