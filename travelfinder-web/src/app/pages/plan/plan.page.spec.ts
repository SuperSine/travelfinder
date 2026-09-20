import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PlanPage } from './plan.page';
import { PlanClient } from '../../core/api/plan-client';
import { PlanningSessionStore } from '../../core/state/planning-session.store';

describe('PlanPage', () => {
  afterEach(() => {
    sessionStorage.removeItem('tf.pending');
  });

  it('has no ion- or ui5- elements and sends through PlanClient', async () => {
    const stream = async function* () {
      yield { type: 'plan_spec' as const, spec: {
        language: 'en-us', areaLabel: 'Singapore', radiusMeters: 5000, categories: [], pointOfInterests: [],
        budgetLevel: 'moderate' as const, dayCount: 1, notes: ''
      }};
      yield { type: 'done' as const };
    };

    const client = jasmine.createSpyObj<PlanClient>('PlanClient', ['stream']);
    client.stream.and.returnValue(stream());

    await TestBed.configureTestingModule({
      imports: [PlanPage],
      providers: [
        provideRouter([]),
        PlanningSessionStore,
        { provide: PlanClient, useValue: client }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PlanPage);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('ion-content')).toBeNull();
    expect(html.querySelector('ui5-textarea')).toBeNull();
    expect(html.querySelector('textarea')).toBeTruthy();
  });

  it('does not stream when needsMapPick and coordinates are missing', async () => {
    sessionStorage.setItem(
      'tf.pending',
      JSON.stringify({
        requestId: 'r1',
        prompt: 'trip to Singapore',
        latitude: null,
        longitude: null,
      })
    );

    const client = jasmine.createSpyObj<PlanClient>('PlanClient', ['stream']);

    await TestBed.configureTestingModule({
      imports: [PlanPage],
      providers: [
        provideRouter([]),
        PlanningSessionStore,
        { provide: PlanClient, useValue: client },
      ],
    }).compileComponents();

    const store = TestBed.inject(PlanningSessionStore);
    spyOn(store, 'run').and.returnValue(Promise.resolve());

    const fixture = TestBed.createComponent(PlanPage);
    fixture.detectChanges();

    expect(store.run).not.toHaveBeenCalled();
    expect(fixture.componentInstance.needsMapPick).toBeTrue();

    fixture.componentInstance.onSend('pick a spot first');

    expect(store.run).not.toHaveBeenCalled();
    expect(fixture.componentInstance.messages).toEqual([
      { role: 'user', content: 'trip to Singapore' },
      { role: 'user', content: 'pick a spot first' },
    ]);
  });
});
