import { ChipType, Substitution } from './manager-live.types';

export interface LeagueLiveRankEntry {
  managerId: number;
  entryName: string;
  playerName: string;
  officialRank: number;
  liveRank: number;
  rankChange: number;
  officialTotal: number;
  liveTotal: number;
  liveGwPoints: number;
  transferCost: number;
  activeChip: ChipType;
  captainElementId: number | null;
  captainName: string | null;
  autoSubs: Substitution[];
  autoSubProjectionFinal: boolean;
  isTiedOnLiveTotal: boolean;
}

export interface LeagueLiveRank {
  leagueId: number;
  leagueName: string;
  eventId: number;
  managerCount: number;
  standings: LeagueLiveRankEntry[];
  calculatedAtUtc: string;
}
