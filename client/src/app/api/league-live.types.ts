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
  previousLiveRank: number | null;
  rankDeltaSincePreviousSnapshot: number;
  rankChangeExplanation: string | null;
}

export interface LeagueLiveRank {
  leagueId: number;
  leagueName: string;
  eventId: number;
  managerCount: number;
  standings: LeagueLiveRankEntry[];
  calculatedAtUtc: string;
}

export interface LeagueEffectiveOwnershipEntry {
  elementId: number;
  webName: string;
  teamId: number;
  elementType: number;
  ownershipPercent: number;
  captaincyPercent: number;
  effectiveOwnershipPercent: number;
  userMultiplier: number;
  rankImpactPerPoint: number;
  impactExplanation: string;
}

export interface LeagueEffectiveOwnership {
  leagueId: number;
  leagueName: string;
  eventId: number;
  managerCount: number;
  selectedManagerId: number | null;
  players: LeagueEffectiveOwnershipEntry[];
  calculatedAtUtc: string;
}
