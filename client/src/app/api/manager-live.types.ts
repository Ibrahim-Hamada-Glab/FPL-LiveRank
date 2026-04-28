export type ChipType = 'None' | 'Wildcard' | 'FreeHit' | 'BenchBoost' | 'TripleCaptain';

export type CaptaincyStatus =
  | 'CaptainPlayed'
  | 'Projected'
  | 'VicePromoted'
  | 'NoCaptainPoints';

export interface ManagerLivePick {
  elementId: number;
  webName: string;
  teamId: number;
  elementType: number;
  position: number;
  multiplier: number;
  isCaptain: boolean;
  isViceCaptain: boolean;
  liveTotalPoints: number;
  minutes: number;
  bonus: number;
  contributedPoints: number;
}

export interface Substitution {
  outElementId: number;
  inElementId: number;
}

export interface ManagerLive {
  managerId: number;
  eventId: number;
  playerName: string;
  teamName: string;
  rawLivePoints: number;
  transferCost: number;
  livePointsAfterHits: number;
  previousTotal: number;
  liveSeasonTotal: number;
  activeChip: ChipType;
  captainElementId: number | null;
  viceCaptainElementId: number | null;
  captaincyStatus: CaptaincyStatus;
  effectiveCaptainElementId: number | null;
  autoSubs: Substitution[];
  blockedStarterElementIds: number[];
  autoSubProjectionFinal: boolean;
  picks: ManagerLivePick[];
  calculatedAtUtc: string;
}

export interface ApiError {
  status: number;
  title: string;
  detail?: string;
  traceId?: string;
}
