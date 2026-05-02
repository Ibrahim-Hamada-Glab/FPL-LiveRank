import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { LeagueLiveService } from '../../api/league-live.service';
import { LeagueEffectiveOwnership } from '../../api/league-live.types';
import { LeagueEffectiveOwnershipComponent } from './league-effective-ownership.component';

describe(LeagueEffectiveOwnershipComponent.name, () => {
  let fixture: ComponentFixture<LeagueEffectiveOwnershipComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LeagueEffectiveOwnershipComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({}),
              queryParamMap: convertToParamMap({}),
            },
          },
        },
        {
          provide: LeagueLiveService,
          useValue: {
            getEffectiveOwnership: () => of(buildEffectiveOwnership()),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LeagueEffectiveOwnershipComponent);
  });

  it('renders the effective ownership table from the API response', () => {
    const component = fixture.componentInstance;
    component.leagueId.set('99');
    component.managerId.set('123');

    component.fetch(false);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Smoke League');
    expect(text).toContain('Captain');
    expect(text).toContain('Positive impact');
  });
});

function buildEffectiveOwnership(): LeagueEffectiveOwnership {
  return {
    leagueId: 99,
    leagueName: 'Smoke League',
    eventId: 7,
    managerCount: 1,
    selectedManagerId: 123,
    calculatedAtUtc: new Date('2026-05-02T00:00:00Z').toISOString(),
    players: [
      {
        elementId: 10,
        webName: 'Captain',
        teamId: 1,
        elementType: 3,
        ownershipPercent: 100,
        captaincyPercent: 0,
        effectiveOwnershipPercent: 100,
        userMultiplier: 2,
        rankImpactPerPoint: 1,
        impactExplanation: 'Positive impact: each point gains against the league.',
      },
    ],
  };
}
