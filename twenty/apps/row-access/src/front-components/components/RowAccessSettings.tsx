import { useState } from 'react';
import { enqueueSnackbar } from 'twenty-sdk/front-component';

import {
  type RowAccessSettingsData,
  saveRowAccessConfig,
  useRowAccessSettings,
} from 'src/front-components/hooks/use-row-access-settings';
import { type RowAccessConfig } from 'src/types/row-access-config.type';
import {
  listRowAccessRuleWarnings,
  normalizeRowAccessConfig,
  removeRule,
  type RowAccessRuleWarning,
  setRuleEnabled,
  setRuleOwnerField,
  setSeeAllRole,
} from 'src/utils/update-row-access-config.util';

const ADMIN_ROLE_LABEL = 'Admin';

const sectionStyle = {
  display: 'flex',
  flexDirection: 'column' as const,
  gap: '8px',
  marginBottom: '24px',
};

const WARNING_TEXT: Record<RowAccessRuleWarning['reason'], string> = {
  'object-unavailable':
    'its object was deleted, deactivated, or cannot hold a rule',
  'owner-field-invalid':
    'its owner field was deleted, deactivated, or is no longer a relation to a workspace member',
  'duplicate-object': 'its object already has a rule',
};

const RowAccessSettingsForm = ({ data }: { data: RowAccessSettingsData }) => {
  const [draftConfig, setDraftConfig] = useState<RowAccessConfig>(
    data.savedConfig,
  );
  const [isSaving, setIsSaving] = useState(false);

  const ruleWarnings = listRowAccessRuleWarnings({
    config: draftConfig,
    candidateObjects: data.candidateObjects,
    ruleObjects: data.ruleObjects,
  });

  const handleSave = async () => {
    setIsSaving(true);

    const isSaved = await saveRowAccessConfig({
      applicationId: data.applicationId,
      config: normalizeRowAccessConfig({
        config: draftConfig,
        roleIds: data.roles.map((role) => role.id),
      }),
    });

    setIsSaving(false);

    enqueueSnackbar({
      message: isSaved
        ? 'Row access saved. It applies within about 10 seconds.'
        : 'Could not save row access.',
      variant: isSaved ? 'success' : 'error',
    });
  };

  return (
    <div>
      {data.savedConfigProblem !== undefined && (
        <p>
          {data.savedConfigProblem} Until you save a valid config, members see
          no rows of any owner object.
        </p>
      )}
      {ruleWarnings.length > 0 && (
        <section style={sectionStyle}>
          <h3>Rules that need attention</h3>
          <p>
            Save keeps these rules. An enabled rule whose custom owner field is
            broken hides every row of every owner object from members until you
            fix it. A hidden standard owner field, such as Account Owner, keeps
            working on the server: rows stay filtered by it. Pick a new owner
            field, or remove the rule if the object should be visible to
            everyone.
          </p>
          <ul>
            {ruleWarnings.map((warning, index) => (
              <li key={`${warning.objectMetadataId}-${index}`}>
                {data.ruleObjects.find(
                  (object) =>
                    object.objectMetadataId === warning.objectMetadataId,
                )?.label ?? `Object ${warning.objectMetadataId}`}
                {warning.isEnabled ? ' (on)' : ' (off)'}:{' '}
                {WARNING_TEXT[warning.reason]}.{' '}
                <button
                  type="button"
                  onClick={() =>
                    setDraftConfig(
                      removeRule(draftConfig, warning.objectMetadataId),
                    )
                  }
                >
                  Remove rule
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}
      <section style={sectionStyle}>
        <h3>Owner-only objects</h3>
        <p>
          A member sees a row of a checked object only when the owner field
          points at them. Rows with no owner are hidden from members.
        </p>
        <table>
          <tbody>
            {data.candidateObjects.map((object) => {
              const rule = draftConfig.rules.find(
                (candidate) =>
                  candidate.objectMetadataId === object.objectMetadataId,
              );
              const isOwnerFieldValid =
                rule === undefined ||
                object.ownerFields.some(
                  (field) =>
                    field.fieldMetadataId === rule.ownerFieldMetadataId,
                );

              return (
                <tr key={object.objectMetadataId}>
                  <td>
                    <label>
                      <input
                        type="checkbox"
                        checked={rule?.isEnabled === true}
                        onChange={(event) =>
                          setDraftConfig(
                            setRuleEnabled(
                              draftConfig,
                              object,
                              event.target.checked,
                            ),
                          )
                        }
                      />
                      {object.label}
                    </label>
                  </td>
                  <td>
                    <select
                      value={
                        rule?.ownerFieldMetadataId ??
                        object.ownerFields[0]?.fieldMetadataId
                      }
                      disabled={rule === undefined}
                      onChange={(event) =>
                        setDraftConfig(
                          setRuleOwnerField(
                            draftConfig,
                            object.objectMetadataId,
                            event.target.value,
                          ),
                        )
                      }
                    >
                      {!isOwnerFieldValid && (
                        <option value={rule?.ownerFieldMetadataId}>
                          Pick an owner field
                        </option>
                      )}
                      {object.ownerFields.map((field) => (
                        <option
                          key={field.fieldMetadataId}
                          value={field.fieldMetadataId}
                        >
                          {field.label}
                        </option>
                      ))}
                    </select>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </section>
      <section style={sectionStyle}>
        <h3>Roles that see every row</h3>
        {data.roles.map((role) => {
          const isAdmin = role.label === ADMIN_ROLE_LABEL;

          return (
            <label key={role.id}>
              <input
                type="checkbox"
                checked={isAdmin || draftConfig.seeAllRoleIds.includes(role.id)}
                disabled={isAdmin}
                onChange={(event) =>
                  setDraftConfig(
                    setSeeAllRole(draftConfig, role.id, event.target.checked),
                  )
                }
              />
              {role.label}
              {isAdmin ? ' (always)' : ''}
            </label>
          );
        })}
      </section>
      <button type="button" disabled={isSaving} onClick={handleSave}>
        {isSaving ? 'Saving…' : 'Save'}
      </button>
    </div>
  );
};

export const RowAccessSettings = () => {
  const state = useRowAccessSettings();

  if (state.kind === 'loading') {
    return <p>Loading row access…</p>;
  }

  if (state.kind === 'error') {
    return <p>{state.message}</p>;
  }

  return <RowAccessSettingsForm data={state.data} />;
};
