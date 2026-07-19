import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  it('caps realtime news at 300 items', () => {
    const component = new DashboardComponent();

    component.prependNews({ id: 301 } as never);

    expect(component.items().length).toBeLessThanOrEqual(300);
  });
});
