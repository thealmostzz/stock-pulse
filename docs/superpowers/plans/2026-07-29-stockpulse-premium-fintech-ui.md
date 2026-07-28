# StockPulse Premium Fintech UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (\`- [ ]\`) syntax for tracking.

**Goal:** Modernize the StockPulse dashboard into an accessible premium-fintech workspace while preserving existing APIs, SignalR updates, and virtual scrolling.

**Architecture:** Keep the dashboard as the state owner. Add a focused inspector component that receives the selected \`NewsItem\`; selection travels from the virtual news feed through a typed output to \`DashboardComponent\`. Apply the visual language through semantic global tokens and focused component styles, keeping the existing standalone Angular component structure.

**Tech Stack:** Angular 22 standalone components, Angular signals/output APIs, SCSS, Angular CDK virtual scrolling, Jasmine/Karma.

## Global Constraints

- Do not change REST endpoints, SignalR payloads, or backend code.
- Preserve \`OnPush\`, \`trackBy\`, event batching, the 300-item in-memory cap, and CDK virtual scrolling.
- Do not add third-party dependencies or web-font requests.
- Keep a dark-only theme with semantic colors; normal text and focus treatment must meet WCAG AA contrast.
- Use system sans-serif for prose and existing monospace typography only for ticker, score, and time values.
- Use 4/8px spacing increments, visible focus styles, 44px controls where practical, and \`prefers-reduced-motion\` fallbacks.
- Do not modify or stage unrelated files, including \`backend/tests/StockPulse.Worker.Tests/MockNewsClientTests.cs\`.

---

## File Structure

| File | Responsibility |
| --- | --- |
| \`frontend/src/styles/_tokens.scss\` | Semantic colors, typography, radius, spacing, and motion tokens. |
| \`frontend/src/styles.scss\` | Global reset, type fallback, focus behavior, and reduced-motion baseline. |
| \`frontend/src/index.html\` | Accurate document language and StockPulse title. |
| \`frontend/src/app/features/dashboard/dashboard.component.{ts,html,scss}\` | Selected-news state and responsive workspace shell. |
| \`frontend/src/app/features/dashboard/news-feed.component.ts\` | Typed selected-news output and feed utility header. |
| \`frontend/src/app/features/dashboard/news-card.component.ts\` | Keyboard-selectable card treatment and selection state. |
| \`frontend/src/app/features/dashboard/news-inspector.component.ts\` | Read-only inspector and safe external source link. |
| \`frontend/src/app/features/dashboard/*component.spec.ts\` | Rendering, selected state, keyboard, output, and inspector coverage. |
| \`frontend/src/app/features/watchlist/watchlist-panel.component.{ts,spec.ts}\` | Visual polish and loading-state accessibility without API changes. |

### Task 1: Establish the dashboard design language and responsive shell

**Files:**
- Modify: \`frontend/src/styles/_tokens.scss\`
- Modify: \`frontend/src/styles.scss\`
- Modify: \`frontend/src/index.html\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.html\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.scss\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.spec.ts\`

**Interfaces:**
- Consumes: Existing \`sp-watchlist-panel\` and \`sp-news-feed\` selectors.
- Produces: \`--sp-accent\`, \`--sp-surface-raised\`, \`--sp-focus-ring\`, \`--sp-radius-md\`, and \`--sp-motion-fast\`.

- [ ] **Step 1: Write the failing responsive-shell test**

\`\`\`ts
it('keeps the watchlist reachable through an accessible disclosure', () => {
  const fixture = TestBed.createComponent(DashboardComponent);
  fixture.detectChanges();

  const summary = fixture.nativeElement.querySelector('summary') as HTMLElement;
  const feed = fixture.nativeElement.querySelector('[aria-label="Realtime news feed"]') as HTMLElement;

  expect(summary.textContent?.trim()).toContain('Watchlist');
  expect(feed).not.toBeNull();
});
\`\`\`

- [ ] **Step 2: Run the test to verify it fails**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/dashboard.component.spec.ts'\`  
Expected: FAIL because the disclosure summary does not exist.

- [ ] **Step 3: Implement the token system and shell**

Define semantic tokens rather than raw colors in component styles:

\`\`\`scss
:root {
  --sp-bg: #08111f;
  --sp-surface: #0d1929;
  --sp-surface-raised: #122137;
  --sp-border: #253a55;
  --sp-text: #edf4ff;
  --sp-muted: #a6b6cc;
  --sp-accent: #63a4ff;
  --sp-focus-ring: #91c0ff;
  --sp-radius-md: .75rem;
  --sp-motion-fast: 180ms;
}
\`\`\`

Wrap the watchlist in an open native disclosure; hide its summary on desktop and show it at narrow widths. Keep the feed as primary content and place the inspector after it in normal document flow. Update title to \`StockPulse | Market intelligence\` and \`lang\` to \`th\`.

- [ ] **Step 4: Run the targeted test and production build**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/dashboard.component.spec.ts'\`  
Expected: PASS.

Run: \`npm.cmd run build --prefix frontend\`  
Expected: PASS with no component-style budget error.

- [ ] **Step 5: Commit**

\`\`\`powershell
git add frontend/src/styles/_tokens.scss frontend/src/styles.scss frontend/src/index.html frontend/src/app/features/dashboard/dashboard.component.html frontend/src/app/features/dashboard/dashboard.component.scss frontend/src/app/features/dashboard/dashboard.component.spec.ts
git commit -m "feat: refresh dashboard workspace shell"
\`\`\`

### Task 2: Add selected-news state and a focused article inspector

**Files:**
- Create: \`frontend/src/app/features/dashboard/news-inspector.component.ts\`
- Create: \`frontend/src/app/features/dashboard/news-inspector.component.spec.ts\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.ts\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.html\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.spec.ts\`

**Interfaces:**
- Consumes: \`NewsItem\` from \`../../core/models/news-item\`.
- Produces: \`selectedNews: WritableSignal<NewsItem | null>\`, \`selectNews(news: NewsItem): void\`, and \`NewsInspectorComponent.news = input<NewsItem | null>(null)\`.

- [ ] **Step 1: Write failing inspector and dashboard-state tests**

\`\`\`ts
it('shows an orientation message when no news is selected', () => {
  const fixture = TestBed.createComponent(NewsInspectorComponent);
  fixture.componentRef.setInput('news', null);
  fixture.detectChanges();

  expect(fixture.nativeElement.textContent).toContain('เลือกข่าวเพื่อดูรายละเอียด');
});

it('stores the news selected from the feed', () => {
  const component = new DashboardComponent();
  const news = createNews(99);

  component.selectNews(news);

  expect(component.selectedNews()).toBe(news);
});
\`\`\`

Add an inspector assertion that its external link is labelled, targets \`_blank\`, and contains \`noopener noreferrer\`.

- [ ] **Step 2: Run the inspector spec to verify it fails**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/news-inspector.component.spec.ts'\`  
Expected: FAIL because \`NewsInspectorComponent\` does not exist.

- [ ] **Step 3: Implement the inspector and selection state**

Create an OnPush standalone inspector with \`news = input<NewsItem | null>(null)\`. It renders source, Thai-formatted time, ticker chips, labelled sentiment/impact, summary, and an explicit \`เปิดบทความต้นฉบับ\` external link. Do not fetch details.

Add:

\`\`\`ts
readonly selectedNews = signal<NewsItem | null>(null);

selectNews(news: NewsItem): void {
  this.selectedNews.set(news);
}
\`\`\`

Import the inspector into the dashboard and bind \`[news]="selectedNews()"\`.

- [ ] **Step 4: Run inspector/dashboard tests and build**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/news-inspector.component.spec.ts'\`  
Expected: PASS.

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/dashboard.component.spec.ts'\`  
Expected: PASS including realtime merge tests.

Run: \`npm.cmd run build --prefix frontend\`  
Expected: PASS.

- [ ] **Step 5: Commit**

\`\`\`powershell
git add frontend/src/app/features/dashboard/news-inspector.component.ts frontend/src/app/features/dashboard/news-inspector.component.spec.ts frontend/src/app/features/dashboard/dashboard.component.ts frontend/src/app/features/dashboard/dashboard.component.html frontend/src/app/features/dashboard/dashboard.component.spec.ts
git commit -m "feat: add selected news inspector"
\`\`\`

### Task 3: Make the live feed and cards selectable, scannable, and accessible

**Files:**
- Create: \`frontend/src/app/features/dashboard/news-card.component.spec.ts\`
- Modify: \`frontend/src/app/features/dashboard/news-card.component.ts\`
- Modify: \`frontend/src/app/features/dashboard/news-feed.component.ts\`
- Modify: \`frontend/src/app/features/dashboard/news-feed.component.spec.ts\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.html\`

**Interfaces:**
- Consumes: \`DashboardComponent.selectNews(news: NewsItem): void\`.
- Produces: \`NewsCardComponent.newsSelected = output<NewsItem>()\`, \`NewsFeedComponent.newsSelected = output<NewsItem>()\`, and \`NewsFeedComponent.selectedNewsId = input<number | null>(null)\`.

- [ ] **Step 1: Write failing card and feed-output tests**

\`\`\`ts
it('emits the article when activated with Enter', () => {
  const fixture = TestBed.createComponent(NewsCardComponent);
  const news = createNews(7);
  fixture.componentRef.setInput('item', news);
  let emitted: NewsItem | undefined;
  fixture.componentInstance.newsSelected.subscribe((item) => emitted = item);
  fixture.detectChanges();

  (fixture.nativeElement.querySelector('.news-card') as HTMLElement)
    .dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));

  expect(emitted).toBe(news);
});
\`\`\`

Add a feed test that listens to \`newsSelected\` and verifies that the feed forwards the rendered card event.

- [ ] **Step 2: Run the card spec to verify it fails**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/news-card.component.spec.ts'\`  
Expected: FAIL because \`newsSelected\` is not defined.

- [ ] **Step 3: Implement typed selection and premium presentation**

\`\`\`ts
readonly newsSelected = output<NewsItem>();

activate(): void {
  this.newsSelected.emit(this.item());
}

onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault();
    this.activate();
  }
}
\`\`\`

Make the article the sole selectable element with \`role="button"\`, \`tabindex="0"\`, \`aria-pressed\`, click and keydown handlers; render the headline as text, because the inspector owns navigation. Use a selected modifier and 2px focus ring. Keep 154px card height and summary clamping. Add the item count to the existing feed header. Bind \`(newsSelected)="selectNews($event)"\` and \`[selectedNewsId]="selectedNews()?.id ?? null"\` in the shell.

- [ ] **Step 4: Run card/feed tests and build**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/news-card.component.spec.ts'\`  
Expected: PASS.

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/dashboard/news-feed.component.spec.ts'\`  
Expected: PASS, including offline status.

Run: \`npm.cmd run build --prefix frontend\`  
Expected: PASS.

- [ ] **Step 5: Commit**

\`\`\`powershell
git add frontend/src/app/features/dashboard/news-card.component.ts frontend/src/app/features/dashboard/news-card.component.spec.ts frontend/src/app/features/dashboard/news-feed.component.ts frontend/src/app/features/dashboard/news-feed.component.spec.ts frontend/src/app/features/dashboard/dashboard.component.html
git commit -m "feat: improve selectable realtime news feed"
\`\`\`

### Task 4: Polish watchlist feedback and complete UI verification

**Files:**
- Modify: \`frontend/src/app/features/watchlist/watchlist-panel.component.ts\`
- Modify: \`frontend/src/app/features/watchlist/watchlist-panel.component.spec.ts\`
- Modify: \`frontend/src/app/features/dashboard/dashboard.component.scss\`
- Modify: \`frontend/src/app/features/dashboard/news-feed.component.ts\`
- Modify: \`frontend/src/app/features/dashboard/news-card.component.ts\`
- Modify: \`frontend/src/app/features/dashboard/news-inspector.component.ts\`

**Interfaces:**
- Consumes: Existing \`WatchlistApiService\` API and Task 1 tokens.
- Produces: No new public data contract; existing add/remove/validation flows remain unchanged.

- [ ] **Step 1: Add a failing loading-label test**

\`\`\`ts
it('disables the add control while a ticker request is in progress', () => {
  const fixture = TestBed.createComponent(WatchlistPanelComponent);
  fixture.componentInstance.isSaving.set(true);
  fixture.detectChanges();

  const button = fixture.nativeElement.querySelector('.watchlist__form button') as HTMLButtonElement;
  expect(button.disabled).toBeTrue();
  expect(button.getAttribute('aria-label')).toContain('กำลังเพิ่มหุ้น');
});
\`\`\`

- [ ] **Step 2: Run the watchlist spec to verify it fails**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless --include='src/app/features/watchlist/watchlist-panel.component.spec.ts'\`  
Expected: FAIL because the loading \`aria-label\` is missing.

- [ ] **Step 3: Implement visual polish without changing behavior**

Retain current form/API calls. Add dynamic add-button \`aria-label\`, 44px minimum controls, descriptive add text when the visual treatment permits, semantic error/state colors, and clear hover/disabled styles. Use \`var(--sp-motion-fast)\` for transitions and reduced-motion fallbacks for shimmer/new-news movement. Do not add an icon package.

- [ ] **Step 4: Run complete verification**

Run: \`npm.cmd test --prefix frontend -- --watch=false --browsers=ChromeHeadless\`  
Expected: PASS.

Run: \`npm.cmd run build --prefix frontend\`  
Expected: PASS with no style-budget error.

Manually inspect at 375px, 768px, 1024px, and 1440px: no horizontal overflow; watchlist disclosure works; keyboard selection updates inspector; external link opens safely; add/remove/error/loading remain usable; reduced-motion suppresses shimmer and new-news movement.

- [ ] **Step 5: Commit**

\`\`\`powershell
git add frontend/src/app/features/watchlist/watchlist-panel.component.ts frontend/src/app/features/watchlist/watchlist-panel.component.spec.ts frontend/src/app/features/dashboard/dashboard.component.scss frontend/src/app/features/dashboard/news-feed.component.ts frontend/src/app/features/dashboard/news-card.component.ts frontend/src/app/features/dashboard/news-inspector.component.ts
git commit -m "feat: polish premium fintech dashboard UI"
\`\`\`
