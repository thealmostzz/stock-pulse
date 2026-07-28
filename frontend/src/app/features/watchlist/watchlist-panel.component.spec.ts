import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { WatchlistItem } from '../../core/models/watchlist-item';
import { WatchlistApiService } from '../../core/services/watchlist-api.service';
import { WatchlistPanelComponent } from './watchlist-panel.component';

describe('WatchlistPanelComponent', () => {
  const add = jasmine.createSpy('add');
  const getAll = jasmine.createSpy('getAll');
  const remove = jasmine.createSpy('remove');

  beforeEach(() => {
    add.calls.reset();
    getAll.calls.reset();
    remove.calls.reset();
    add.and.returnValue(of(createWatchlistItem(1, 'A12345678901234567890')));
    getAll.and.returnValue(of([]));
    remove.and.returnValue(of(void 0));

    TestBed.configureTestingModule({
      providers: [{
        provide: WatchlistApiService,
        useValue: { getAll, add, remove },
      }],
    });
  });

  it('publishes active tickers only after the initial load succeeds', () => {
    getAll.and.returnValue(of([
      createWatchlistItem(1, 'AAPL'),
      createWatchlistItem(2, 'MSFT', false),
    ]));
    const fixture = TestBed.createComponent(WatchlistPanelComponent);
    const emissions: (readonly string[])[] = [];
    fixture.componentInstance.activeTickersChanged.subscribe((tickers: readonly string[]) => emissions.push(tickers));

    fixture.detectChanges();

    expect(emissions).toEqual([['AAPL']]);
  });

  it('publishes the updated active tickers after an add succeeds', () => {
    getAll.and.returnValue(of([createWatchlistItem(1, 'AAPL')]));
    add.and.returnValue(of(createWatchlistItem(2, 'MSFT')));
    const fixture = TestBed.createComponent(WatchlistPanelComponent);
    const emissions: (readonly string[])[] = [];
    fixture.componentInstance.activeTickersChanged.subscribe((tickers: readonly string[]) => emissions.push(tickers));
    fixture.detectChanges();
    fixture.componentInstance.ticker = 'MSFT';

    fixture.componentInstance.addTicker();

    expect(emissions).toEqual([['AAPL'], ['AAPL', 'MSFT']]);
  });

  it('publishes the remaining active tickers after a remove succeeds', () => {
    const aapl = createWatchlistItem(1, 'AAPL');
    const msft = createWatchlistItem(2, 'MSFT');
    getAll.and.returnValue(of([aapl, msft]));
    const fixture = TestBed.createComponent(WatchlistPanelComponent);
    const emissions: (readonly string[])[] = [];
    fixture.componentInstance.activeTickersChanged.subscribe((tickers: readonly string[]) => emissions.push(tickers));
    fixture.detectChanges();

    fixture.componentInstance.removeTicker(aapl);

    expect(remove).toHaveBeenCalledWith('AAPL');
    expect(emissions).toEqual([['AAPL', 'MSFT'], ['MSFT']]);
  });

  it('rejects slash tickers locally without calling the API', () => {
    const component = createComponent();
    component.ticker = 'BRK/B';

    component.addTicker();

    expect(add).not.toHaveBeenCalled();
    expect(component.errorMessage()).toContain('A-Z');
  });

  it('submits a valid 20-character ticker', () => {
    const component = createComponent();
    component.ticker = 'a1234567890123456789';

    component.addTicker();

    expect(add).toHaveBeenCalledWith({ ticker: 'A1234567890123456789', displayName: null, market: null });
  });

  it('disables the add control while a ticker request is in progress', () => {
    const fixture = TestBed.createComponent(WatchlistPanelComponent);
    fixture.componentInstance.isSaving.set(true);
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('.watchlist__form button') as HTMLButtonElement;
    expect(button.disabled).toBeTrue();
    expect(button.getAttribute('aria-label')).toContain('กำลังเพิ่มหุ้น');
  });

  it('uses semantic mono typography for ticker values but not descriptions', () => {
    const fixture = TestBed.createComponent(WatchlistPanelComponent);
    fixture.detectChanges();
    fixture.componentInstance.isLoading.set(false);
    fixture.componentInstance.items.set([{
      id: 1,
      ticker: 'NVDA',
      displayName: 'NVIDIA',
      market: 'NASDAQ',
      isActive: true,
      sortOrder: 0,
    }]);
    fixture.detectChanges();

    const ticker = fixture.nativeElement.querySelector('strong') as HTMLElement;
    const description = fixture.nativeElement.querySelector('small') as HTMLElement;

    expect(ticker.classList.contains('sp-mono')).toBeTrue();
    expect(description.classList.contains('sp-mono')).toBeFalse();
  });
});

function createComponent(): WatchlistPanelComponent {
  const fixture = TestBed.createComponent(WatchlistPanelComponent);
  fixture.detectChanges();
  return fixture.componentInstance;
}

function createWatchlistItem(id: number, ticker: string, isActive = true): WatchlistItem {
  return {
    id,
    ticker,
    displayName: null,
    market: null,
    sortOrder: id,
    isActive,
  };
}
