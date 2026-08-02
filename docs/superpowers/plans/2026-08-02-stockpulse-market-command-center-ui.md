# StockPulse Market Command Center Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (\`- [ ]\`) syntax for tracking.

**Goal:** Rebuild StockPulse's dashboard hierarchy as a dark Market Command Center with a full-width app bar, SaaS watchlist rail, card-based news workspace, and prominent insight panel.

**Architecture:** Keep \`DashboardComponent\` as the orchestration boundary for responsive watchlist visibility, selected news, and existing query/realtime state. Add a focused presentational top-bar component and extend existing dashboard, feed, card, inspector, and watchlist components rather than changing API services or backend contracts. All state changes remain signals/typed outputs and the virtualized news stream remains a single column.

**Tech Stack:** Angular 22 standalone components, signals/input/output APIs, Angular CDK virtual scrolling, SCSS, Jasmine/Karma.

## Global Constraints

- Do not change REST endpoints, SignalR payloads, query semantics, pagination, sort/filter behavior, or backend code.
- Preserve \`OnPush\`, \`trackBy\`, CDK virtual scrolling, 154px virtual-row height, batched realtime updates, and the 300-item cap.
- Keep the dashboard dark-only; do not add dependencies, web fonts, chart data, or price data.
- Use blue only for action/selection/focus; retain green/amber/red for positive/neutral-or-warning/negative-or-error states.
- Use system sans-serif for prose and \`.sp-mono\` only for tickers, counts, scores, and timestamps.
- Every interactive control must have a visible focus state, an accessible name, and a practical minimum 44px pointer target.
- Use 4/8px spacing, semantic tokens, 160–220ms opacity/transform/color transitions only, and \`prefers-reduced-motion\` fallbacks.
- At 1200px show rail/workspace/insight; at 768–1199px move insight below workspace; below 768px keep the workspace first and retain an accessible route to Watchlist.
- Do not stage or modify unrelated files.

---

## File Structure

| File | Responsibility |
| --- | --- |
| \`frontend/src/app/features/dashboard/dashboard-top-bar.component.ts\` | Presentational full-width app bar; emits a request to focus/open Watchlist. |
| \`frontend/src/app/features/dashboard/dashboard-top-bar.component.spec.ts\` | App-bar labels, live status, count, and accessible action coverage. |
| \`frontend/src/app/features/dashboard/dashboard.component.{ts,html,scss,spec.ts}\` | Dashboard shell, responsive Watchlist state, top-bar orchestration, skip link, and layout hierarchy. |
| \`frontend/src/app/features/watchlist/watchlist-panel.component.{ts,spec.ts}\` | Exposes a narrow public focus method; restyles Watchlist as a SaaS rail without duplicating mutations. |
| \`frontend/src/app/features/dashboard/news-feed.component.{ts,spec.ts}\` | Command-center workspace header and status strip while retaining filter/sort/query outputs. |
| \`frontend/src/app/features/dashboard/news-card.component.{ts,spec.ts}\` | Elevated card presentation and semantic selected/focus states without changing activation behavior. |
| \`frontend/src/app/features/dashboard/news-inspector.component.{ts,spec.ts}\` | Raised Insight panel visual hierarchy and selected/empty state coverage. |
| \`frontend/src/styles/_tokens.scss\` | Additional semantic surface, spacing, and elevation tokens. |
| \`frontend/src/styles.scss\` | Skip-link styling and global reduced-motion support. |

### Task 1: Add an accessible command-center top bar and dashboard shell

**Files:**
- Create: \`frontend/src/app/features/dashboard/dashboard-top-bar.component.ts\`
- Create: \`frontend/src/app/features/dashboard/dashboard-top-bar.component.spec.ts\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.ts\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.html\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.scss\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.spec.ts\`
- Modify: \`frontend/src/styles.scss\`

**Interfaces:**
- Produces: \`DashboardTopBarComponent.watchlistRequested = output<void>()\`.
- Consumes: \`HubConnectionState\`, \`DashboardComponent.connectionState()\`, and \`DashboardComponent.activeWatchlistTickers()\`.
- Produces: \`DashboardComponent.openWatchlist(): void\`, which opens the native \`HTMLDetailsElement\` and calls \`WatchlistPanelComponent.focusTickerInput()\`.

- [ ] **Step 1: Write failing top-bar and shell tests**

Create a top-bar test:

\`\`\`ts
it('emits a labelled request to manage the watchlist', () => {
  const fixture = TestBed.createComponent(DashboardTopBarComponent);
  let requested = false;
  fixture.componentInstance.watchlistRequested.subscribe(() => requested = true);
  fixture.componentRef.setInput('connectionState', HubConnectionState.Connected);
  fixture.componentRef.setInput('watchlistCount', 2);
  fixture.detectChanges();

  const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
  button.click();

  expect(button.getAttribute('aria-label')).toBe('จัดการ Watchlist');
  expect(requested).toBeTrue();
});
\`\`\`

Add a dashboard test that sets a fake disclosure and watchlist panel, calls \`openWatchlist()\`, and asserts \`open === true\` plus \`focusTickerInput\` was called.

- [ ] **Step 2: Run the new test to verify it fails**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/dashboard-top-bar.component.spec.ts'\`  
Expected: FAIL because \`DashboardTopBarComponent\` does not exist.

- [ ] **Step 3: Implement the top bar and shell hierarchy**

Implement an OnPush standalone top bar with exact inputs and output:

\`\`\`ts
readonly connectionState = input(HubConnectionState.Disconnected);
readonly watchlistCount = input(0);
readonly watchlistRequested = output<void>();
\`\`\`

Render a product mark, workspace label, live status text, watchlist count using \`.sp-mono\`, and one button labelled \`จัดการ Watchlist\`. In the dashboard, add a skip link targeting \`#market-workspace\`, bind the top-bar output to \`openWatchlist()\`, add template references \`#watchlistDisclosure\` and \`#watchlistPanel\`, and use \`@ViewChild\` references. Update the grid so the top bar spans all columns and the dashboard body owns rail/workspace/insight placement. At narrow width, opening Watchlist must remain possible.

- [ ] **Step 4: Run targeted tests and build**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/dashboard-top-bar.component.spec.ts'\`  
Expected: PASS.

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/dashboard.component.spec.ts'\`  
Expected: PASS, including existing realtime/query coverage.

Run: \`npm.cmd run build --prefix frontend\`  
Expected: PASS without a component-style budget error.

- [ ] **Step 5: Commit**

\`\`\`powershell
git add frontend/src/app/features/dashboard/dashboard-top-bar.component.ts frontend/src/app/features/dashboard/dashboard-top-bar.component.spec.ts frontend/src/app/features/dashboard/dashboard.component.ts frontend/src/app/features/dashboard/dashboard.component.html frontend/src/app/features/dashboard/dashboard.component.scss frontend/src/app/features/dashboard/dashboard.component.spec.ts frontend/src/styles.scss
git commit -m "feat: add market command center shell"
\`\`\`

### Task 2: Convert Watchlist into a compact SaaS rail

**Files:**
- Modify: \`frontend/src/app/features/watchlist/watchlist-panel.component.ts\`
- Modify: \`frontend/src/app/features/watchlist/watchlist-panel.component.spec.ts\`
- Modify: \`frontend/src/styles/_tokens.scss\`

**Interfaces:**
- Consumes: existing \`WatchlistApiService\` and current add/remove validation flow.
- Produces: \`focusTickerInput(): void\` on \`WatchlistPanelComponent\`; no new public API model or HTTP request.
- Consumes: top-bar invocation from Task 1 through the parent component's \`@ViewChild\`.

- [ ] **Step 1: Write a failing form-focus test**

Add this Jasmine test:

\`\`\`ts
it('focuses the ticker field when the dashboard requests watchlist management', () => {
  const fixture = TestBed.createComponent(WatchlistPanelComponent);
  fixture.detectChanges();
  const input = fixture.nativeElement.querySelector('#watchlist-ticker') as HTMLInputElement;
  spyOn(input, 'focus');

  fixture.componentInstance.focusTickerInput();

  expect(input.focus).toHaveBeenCalled();
});
\`\`\`

- [ ] **Step 2: Run the spec to verify it fails**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/watchlist/watchlist-panel.component.spec.ts'\`  
Expected: FAIL because \`focusTickerInput\` is not defined.

- [ ] **Step 3: Implement focused management and rail styling**

Use \`viewChild<ElementRef<HTMLInputElement>>('tickerInput')\` or an equivalent stable Angular query. Add \`#tickerInput\` to the existing input and implement:

\`\`\`ts
focusTickerInput(): void {
  this.tickerInput()?.nativeElement.focus();
}
\`\`\`

Retain the current \`addTicker\`, \`removeTicker\`, errors, signals, and output. Restyle the panel with an explicit rail heading, compact add action, raised ticker rows, a count label, and semantic hover/focus/disabled states. Add only semantic tokens such as \`--sp-surface-sunken\`, \`--sp-shadow-panel\`, and \`--sp-space-3\`; consume tokens in component CSS rather than introducing raw shared colors.

- [ ] **Step 4: Run tests and build**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/watchlist/watchlist-panel.component.spec.ts'\`  
Expected: PASS including existing ticker validation and API behavior.

Run: \`npm.cmd run build --prefix frontend\`  
Expected: PASS.

- [ ] **Step 5: Commit**

\`\`\`powershell
git add frontend/src/app/features/watchlist/watchlist-panel.component.ts frontend/src/app/features/watchlist/watchlist-panel.component.spec.ts frontend/src/styles/_tokens.scss
git commit -m "feat: refresh watchlist navigation rail"
\`\`\`

### Task 3: Rebuild the news workspace as a card-based command stream

**Files:**
- Modify: \`frontend/src/app/features/dashboard/news-feed.component.ts\`
- Modify: \`frontend/src/app/features/dashboard/news-feed.component.spec.ts\`
- Modify: \`frontend/src/app/features/dashboard/news-card.component.ts\`
- Modify: \`frontend/src/app/features/dashboard/news-card.component.spec.ts\`
- Modify: \`frontend/src/app/features/dashboard/news-filter.component.ts\`

**Interfaces:**
- Consumes: existing \`NewsQuery\`, \`NewsItem\`, \`newsSelected\`, \`queryChanged\`, \`clearRequested\`, \`loadMore\`, and connection-state inputs/outputs.
- Produces: no new API or domain interface. \`itemSize\` stays \`154\`.
- Preserves: \`onScrolledIndexChange\`, selected card \`aria-pressed\`, Enter/Space activation, and current filter/sort values.

- [ ] **Step 1: Write failing workspace-state tests**

Extend the feed spec:

\`\`\`ts
it('shows the command status strip with the active item count', () => {
  const fixture = TestBed.createComponent(NewsFeedComponent);
  fixture.componentRef.setInput('items', [createNews(1), createNews(2)]);
  fixture.componentRef.setInput('query', defaultQuery);
  fixture.componentRef.setInput('totalCount', 7);
  fixture.detectChanges();

  expect(fixture.nativeElement.querySelector('.news-feed__status-strip')?.textContent)
    .toContain('2 / 7');
});
\`\`\`

Extend the card spec to assert the selected modifier and \`aria-pressed="true"\` when \`isSelected\` is true.

- [ ] **Step 2: Run the specs to verify they fail**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/news-feed.component.spec.ts'\`  
Expected: FAIL because the command status strip does not exist.

- [ ] **Step 3: Implement command workspace hierarchy**

Rearrange feed markup into page header, filter row, status strip, and scroll region. The strip shows the explicit live/offline label, active filter count derived from the existing query, and \`{{ items().length }} / {{ announcedTotalCount() }}\`. Keep the existing Thai loading/error/empty copy.

Make card surfaces visually distinct with a 12px radius, 8px gaps, a raised surface token, and a 3px accent selection rail. Do not change card activation, virtual item height, summary clamp, or signal meaning. Restyle filter controls into a compact command bar with labels preserved and 44px controls; focus must use \`--sp-focus-ring\`, not \`--sp-positive\`.

- [ ] **Step 4: Run focused tests and build**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/news-feed.component.spec.ts'\`  
Expected: PASS.

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/news-card.component.spec.ts'\`  
Expected: PASS.

Run: \`npm.cmd run build --prefix frontend\`  
Expected: PASS.

- [ ] **Step 5: Commit**

\`\`\`powershell
git add frontend/src/app/features/dashboard/news-feed.component.ts frontend/src/app/features/dashboard/news-feed.component.spec.ts frontend/src/app/features/dashboard/news-card.component.ts frontend/src/app/features/dashboard/news-card.component.spec.ts frontend/src/app/features/dashboard/news-filter.component.ts
git commit -m "feat: redesign live news workspace"
\`\`\`

### Task 4: Elevate the insight panel and complete responsive/accessibility verification

**Files:**
- Modify: \`frontend/src/app/features/dashboard/news-inspector.component.ts\`
- Modify: \`frontend/src/app/features/dashboard/news-inspector.component.spec.ts\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.scss\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.spec.ts\`

**Interfaces:**
- Consumes: \`NewsItem | null\` through the existing \`news\` input.
- Produces: no output, API call, or new data model.
- Preserves: explicit \`target="_blank"\` and \`rel="noopener noreferrer"\` external link behavior.

- [ ] **Step 1: Write failing insight-panel tests**

Add:

\`\`\`ts
it('labels the selected article analysis region', () => {
  const fixture = TestBed.createComponent(NewsInspectorComponent);
  fixture.componentRef.setInput('news', createNews(1));
  fixture.detectChanges();

  const panel = fixture.nativeElement.querySelector('[aria-label="Selected news insight"]') as HTMLElement;
  expect(panel).not.toBeNull();
  expect(panel.textContent).toContain('Impact');
});
\`\`\`

- [ ] **Step 2: Run the spec to verify it fails**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/news-inspector.component.spec.ts'\`  
Expected: FAIL because the selected insight landmark has not been added.

- [ ] **Step 3: Implement the analysis panel and responsive detail placement**

Turn the inspector into a labelled raised panel with a compact header, metadata, key-value signal summary, ticker chips, and a full-width external article action. Keep the empty state instructional.

Ensure desktop insight is sticky within its column only when there is enough viewport height; avoid nested scrolling. At 768–1199px it follows the workspace in normal flow; below 768px it follows the selected feed. Add \`@media (prefers-reduced-motion: reduce)\` overrides for any new transition. Preserve the Watchlist reopen behavior across breakpoints.

- [ ] **Step 4: Run full frontend verification**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless\`  
Expected: PASS.

Run: \`npm.cmd run build --prefix frontend\`  
Expected: PASS without style-budget warnings.

Manually verify 375px, 768px, 1024px, and 1440px: skip link; top-bar Watchlist action; add/remove validation; filter/sort; selected card → insight panel; safe external link; active/offline labels; no horizontal overflow; reduced-motion behavior.

- [ ] **Step 5: Commit**

\`\`\`powershell
git add frontend/src/app/features/dashboard/news-inspector.component.ts frontend/src/app/features/dashboard/news-inspector.component.spec.ts frontend/src/app/features/dashboard/dashboard.component.scss frontend/src/app/features/dashboard/dashboard.component.spec.ts
git commit -m "feat: elevate selected news insight panel"
\`\`\`
