import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';

import { FplLiveHubService } from '../../api/fpl-live-hub.service';
import { LeagueLiveService } from '../../api/league-live.service';
import { LeagueLiveRank } from '../../api/league-live.types';
import { LeagueLiveComponent } from './league-live.component';

describe(LeagueLiveComponent.name, () => {
  let fixture: ComponentFixture<LeagueLiveComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LeagueLiveComponent],
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
            getLive: () => of(buildLeagueLiveRank()),
            refresh: () => of(buildLeagueLiveRank()),
          },
        },
        {
          provide: FplLiveHubService,
          useValue: {
            connectionStatus: signal('disconnected'),
            leagueUpdates$: new Subject<LeagueLiveRank>().asObservable(),
            eventRefreshes$: new Subject().asObservable(),
            refreshProgress$: new Subject().asObservable(),
            joinLeague: () => Promise.resolve(),
            leaveLeague: () => Promise.resolve(),
            joinEvent: () => Promise.resolve(),
            leaveEvent: () => Promise.resolve(),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LeagueLiveComponent);
  });

  it('renders a live league table from the API response', () => {
    const component = fixture.componentInstance;
    component.leagueId.set('99');

    component.fetch(false);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Smoke League');
    expect(text).toContain('Smoke FC');
    expect(text).toContain('Smoke Manager');
    expect(text).toContain('Effective ownership');
  });
});

function buildLeagueLiveRank(): LeagueLiveRank {
  return {
    leagueId: 99,
    leagueName: 'Smoke League',
    eventId: 7,
    managerCount: 1,
    calculatedAtUtc: new Date('2026-05-02T00:00:00Z').toISOString(),
    standings: [
      {
        managerId: 123,
        entryName: 'Smoke FC',
        playerName: 'Smoke Manager',
        officialRank: 2,
        liveRank: 1,
        rankChange: 1,
        officialTotal: 100,
        liveTotal: 138,
        liveGwPoints: 38,
        transferCost: 4,
        activeChip: 'None',
        captainElementId: 10,
        captainName: 'Captain',
        autoSubs: [],
        autoSubProjectionFinal: true,
        isTiedOnLiveTotal: false,
        previousLiveRank: 2,
        rankDeltaSincePreviousSnapshot: 1,
        rankChangeExplanation: 'Up 1 since the previous live snapshot.',
      },
    ],
  };
}
