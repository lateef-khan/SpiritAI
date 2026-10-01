import { useEffect, useState } from 'react';
import { MetadataApiClient } from 'twenty-client-sdk/metadata';
import { useFrontComponentId } from 'twenty-sdk/front-component';

import { OWN_APPLICATION_UNIVERSAL_IDENTIFIERS } from 'src/constants/own-application-universal-identifiers.constant';
import { ROW_ACCESS_CONFIG_VARIABLE_KEY } from 'src/constants/row-access-config-variable-key.constant';
import {
  type MetadataObject,
  type MetadataRole,
} from 'src/types/metadata-object.type';
import {
  type OwnerCandidateObject,
  type RuleObject,
} from 'src/types/owner-candidate-object.type';
import { type RowAccessConfig } from 'src/types/row-access-config.type';
import {
  listOwnerCandidateObjects,
  listRuleObjects,
} from 'src/utils/list-owner-candidate-objects.util';
import { parseRowAccessConfig } from 'src/utils/parse-row-access-config.util';

export type RowAccessSettingsData = {
  applicationId: string;
  savedConfig: RowAccessConfig;
  savedConfigProblem: string | undefined;
  candidateObjects: OwnerCandidateObject[];
  ruleObjects: RuleObject[];
  roles: MetadataRole[];
};

export type RowAccessSettingsState =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; data: RowAccessSettingsData };

// An app that is not installed has no id here, and its objects do not exist.
const findOwnApplicationIds = async (
  client: MetadataApiClient,
): Promise<string[]> => {
  const ids = await Promise.all(
    OWN_APPLICATION_UNIVERSAL_IDENTIFIERS.map(async (universalIdentifier) => {
      try {
        const result = await client.query({
          findOneApplication: { __args: { universalIdentifier }, id: true },
        });

        return result.findOneApplication?.id;
      } catch {
        return undefined;
      }
    }),
  );

  return ids.filter((id): id is string => typeof id === 'string');
};

const LOAD_ERROR_MESSAGE =
  'Could not load the row access settings. You need the Applications and Roles permissions.';

// Each call runs with the signed-in user's role intersected with the app role:
// frontComponent and currentWorkspace (any user), findOneApplication (also
// for our own apps' ids) and updateOneApplicationVariable (APPLICATIONS), getRoles (ROLES), objects
// (metadata read).
const loadRowAccessSettings = async (
  frontComponentId: string,
): Promise<RowAccessSettingsData> => {
  const client = new MetadataApiClient();

  const frontComponentResult = await client.query({
    frontComponent: { __args: { id: frontComponentId }, applicationId: true },
  });

  const applicationId = frontComponentResult.frontComponent?.applicationId;

  if (typeof applicationId !== 'string' || applicationId === '') {
    throw new Error('No application id');
  }

  const [
    applicationResult,
    objectsResult,
    rolesResult,
    workspaceResult,
    ownApplicationIds,
  ] = await Promise.all([
      client.query({
        findOneApplication: {
          __args: { id: applicationId },
          applicationVariables: { key: true, value: true },
        },
      }),
      client.query({
        objects: {
          __args: { paging: { first: 1000 }, filter: {} },
          edges: {
            node: {
              id: true,
              universalIdentifier: true,
              applicationId: true,
              nameSingular: true,
              labelSingular: true,
              isSystem: true,
              isActive: true,
              fieldsList: {
                id: true,
                name: true,
                label: true,
                type: true,
                isActive: true,
                relation: {
                  type: true,
                  targetObjectMetadata: { nameSingular: true },
                },
              },
            },
          },
        },
      }),
      client.query({ getRoles: { id: true, label: true } }),
      client.query({
        currentWorkspace: { workspaceCustomApplicationId: true },
      }),
      findOwnApplicationIds(client),
    ]);

  const storedValue =
    applicationResult.findOneApplication?.applicationVariables?.find(
      (variable) => variable.key === ROW_ACCESS_CONFIG_VARIABLE_KEY,
    )?.value;

  const { config, problem } = parseRowAccessConfig(storedValue);

  const objects = (objectsResult.objects?.edges ?? []).map(
    (edge) => edge.node as unknown as MetadataObject,
  );
  const workspaceCustomApplicationId =
    workspaceResult.currentWorkspace?.workspaceCustomApplicationId ??
    undefined;

  return {
    applicationId,
    savedConfig: config,
    savedConfigProblem: problem,
    candidateObjects: listOwnerCandidateObjects(
      objects,
      workspaceCustomApplicationId,
      ownApplicationIds,
    ),
    ruleObjects: listRuleObjects(
      objects,
      workspaceCustomApplicationId,
      ownApplicationIds,
    ),
    roles: (rolesResult.getRoles ?? []).map((role) => ({
      id: role.id,
      label: role.label,
    })),
  };
};

export const saveRowAccessConfig = async ({
  applicationId,
  config,
}: {
  applicationId: string;
  config: RowAccessConfig;
}): Promise<boolean> => {
  try {
    const client = new MetadataApiClient();

    await client.mutation({
      updateOneApplicationVariable: {
        __args: {
          key: ROW_ACCESS_CONFIG_VARIABLE_KEY,
          value: JSON.stringify(config),
          applicationId,
        },
      },
    });

    return true;
  } catch {
    return false;
  }
};

export const useRowAccessSettings = (): RowAccessSettingsState => {
  const frontComponentId = useFrontComponentId();
  const [state, setState] = useState<RowAccessSettingsState>({
    kind: 'loading',
  });

  useEffect(() => {
    let isCancelled = false;

    loadRowAccessSettings(frontComponentId)
      .then((data) => {
        if (!isCancelled) {
          setState({ kind: 'ready', data });
        }
      })
      .catch(() => {
        if (!isCancelled) {
          setState({ kind: 'error', message: LOAD_ERROR_MESSAGE });
        }
      });

    return () => {
      isCancelled = true;
    };
  }, [frontComponentId]);

  return state;
};
