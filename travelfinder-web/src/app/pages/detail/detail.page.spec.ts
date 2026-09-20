import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DetailPage } from './detail.page';
import { PlanningSessionStore } from '../../core/state/planning-session.store';

describe('DetailPage', () => {
  it('renders store itinerary without ion- or ui5-', async () => {
    const store = new PlanningSessionStore();
    store.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    store.apply({
      type: 'plan_delta',
      stop: { dayIndex: 0, stopIndex: 0, placeId: 'google:1', name: 'Fort Canning', reason: 'Shade' }
    });

    await TestBed.configureTestingModule({
      imports: [DetailPage],
      providers: [provideRouter([]), { provide: PlanningSessionStore, useValue: store }]
    }).compileComponents();

    const fixture = TestBed.createComponent(DetailPage);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('ion-header')).toBeNull();
    expect(html.querySelector('ui5-dynamic-page')).toBeNull();
    expect(html.textContent).toContain('Fort Canning');
  });
});
