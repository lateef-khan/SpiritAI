import { EMPTY_ROW_ACCESS_CONFIG } from 'src/constants/empty-row-access-config.constant';
import {
  type RowAccessConfig,
  type RowAccessRule,
} from 'src/types/row-access-config.type';

export type ParsedRowAccessConfig = {
  config: RowAccessConfig;
  problem: string | undefined;
};

const isRule = (value: unknown): value is RowAccessRule => {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const rule = value as Record<string, unknown>;

  return (
    typeof rule.objectMetadataId === 'string' &&
    typeof rule.ownerFieldMetadataId === 'string' &&
    typeof rule.isEnabled === 'boolean'
  );
};

// Lenient on purpose: the page must open even on a broken value so the admin
// can fix it. The server stays strict and fails closed until a valid value is
// saved.
export const parseRowAccessConfig = (
  rawValue: string | undefined,
): ParsedRowAccessConfig => {
  if (rawValue === undefined || rawValue.trim() === '') {
    return {
      config: EMPTY_ROW_ACCESS_CONFIG,
      problem: 'No config is saved yet.',
    };
  }

  let parsed: unknown;

  try {
    parsed = JSON.parse(rawValue);
  } catch {
    return {
      config: EMPTY_ROW_ACCESS_CONFIG,
      problem: 'The saved config is not valid JSON.',
    };
  }

  const candidate = parsed as Partial<Record<keyof RowAccessConfig, unknown>>;

  if (
    typeof parsed !== 'object' ||
    parsed === null ||
    candidate.version !== 1 ||
    !Array.isArray(candidate.rules) ||
    !candidate.rules.every(isRule) ||
    !Array.isArray(candidate.seeAllRoleIds) ||
    !candidate.seeAllRoleIds.every((roleId) => typeof roleId === 'string')
  ) {
    return {
      config: EMPTY_ROW_ACCESS_CONFIG,
      problem: 'The saved config has the wrong shape.',
    };
  }

  return {
    config: {
      version: 1,
      rules: candidate.rules.map((rule) => ({
        objectMetadataId: rule.objectMetadataId,
        ownerFieldMetadataId: rule.ownerFieldMetadataId,
        isEnabled: rule.isEnabled,
      })),
      seeAllRoleIds: [...(candidate.seeAllRoleIds as string[])],
    },
    problem: undefined,
  };
};
