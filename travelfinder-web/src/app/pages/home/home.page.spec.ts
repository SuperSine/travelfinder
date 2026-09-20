import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HomePage } from './home.page';

describe('HomePage', () => {
  let fixture: ComponentFixture<HomePage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(HomePage);
    fixture.detectChanges();
  });

  it('should create without ionic or ui5 elements', () => {
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelectorAll('*')).toBeTruthy();
    expect(html.querySelector('ion-content')).toBeNull();
    expect(html.querySelector('ion-header')).toBeNull();
    expect(html.querySelector('ui5-button')).toBeNull();
    expect(html.querySelector('textarea')).toBeTruthy();
    expect(html.querySelector('button[data-role="start-plan"]')).toBeTruthy();
  });
});
