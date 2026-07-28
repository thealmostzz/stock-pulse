import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { WatchlistApiService } from '../../core/services/watchlist-api.service';
import { WatchlistPanelComponent } from './watchlist-panel.component';

describe('WatchlistPanelComponent', () => {
  const add = jasmine.createSpy('add');

  beforeEach(() => {
    add.calls.reset();
    add.and.returnValue(of({ id: 1, ticker: 'A12345678901234567890', displayName: null, market: null, isActive: true }));

    TestBed.configureTestingModule({
      providers: [{
        provide: WatchlistApiService,
        useValue: { getAll: () => of([]), add, remove: () => of(void 0) },
      }],
    });
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
