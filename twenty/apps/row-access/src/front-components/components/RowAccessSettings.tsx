import 'twenty-ui/style.css';

import { useState } from 'react';
import { enqueueSnackbar, useColorScheme } from 'twenty-sdk/front-component';
import { Callout } from 'twenty-ui/primitives/feedback';
import { Button } from 'twenty-ui/primitives/input';
import { Section } from 'twenty-ui/primitives/layout';
import { Card, CardContent } from 'twenty-ui/primitives/surfaces';
import { H2Title } from 'twenty-ui/primitives/typography';
import { ThemeProvider } from 'twenty-ui/theme-constants';

import { getRowAccessSettingsStyles } from 'src/constants/row-access-settings-styles.constant';
import { OwnerFieldSelect } from 'src/front-components/components/OwnerFieldSelect';
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
  const styles = getRowAccessSettingsStyles();
  const colorScheme = useColorScheme();

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
    <div style={{ ...styles.page, colorScheme }}>
      {data.savedConfigProblem !== undefined && (
        <Callout
          variant="warning"
          title="The saved row access config is invalid"
          description={`${data.savedConfigProblem} Until you save a valid config, members see no rows of any owner object.`}
        />
      )}
      {ruleWarnings.length > 0 && (
        <Section>
          <H2Title
            title="Rules that need attention"
            description="Save keeps these rules. An enabled rule whose custom owner field is broken hides every row of every owner object from members until you fix it. A hidden standard owner field, such as Account Owner, keeps working on the server: rows stay filtered by it. Pick a new owner field, or remove the rule if the object should be visible to everyone."
          />
          <Card rounded>
            {ruleWarnings.map((warning, index) => (
              <CardContent
                key={`${warning.objectMetadataId}-${index}`}
                divider={index < ruleWarnings.length - 1}
                style={styles.row}
              >
                <span style={styles.rowLabel}>
                  {data.ruleObjects.find(
                    (object) =>
                      object.objectMetadataId === warning.objectMetadataId,
                  )?.label ?? `Object ${warning.objectMetadataId}`}
                  {warning.isEnabled ? ' (on)' : ' (off)'}:{' '}
                  {WARNING_TEXT[warning.reason]}.
                </span>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  color="danger"
                  onClick={() =>
                    setDraftConfig(
                      removeRule(draftConfig, warning.objectMetadataId),
                    )
                  }
                >
                  Remove rule
                </Button>
              </CardContent>
            ))}
          </Card>
        </Section>
      )}
      <Section>
        <H2Title
          title="Owner-only objects"
          description="A member sees a row of a checked object only when the owner field points at them. Rows with no owner are hidden from members."
        />
        <Card rounded>
          {data.candidateObjects.map((object, index) => {
            const rule = draftConfig.rules.find(
              (candidate) =>
                candidate.objectMetadataId === object.objectMetadataId,
            );
            const isOwnerFieldValid =
              rule === undefined ||
              object.ownerFields.some(
                (field) => field.fieldMetadataId === rule.ownerFieldMetadataId,
              );

            return (
              <CardContent
                key={object.objectMetadataId}
                divider={index < data.candidateObjects.length - 1}
                style={styles.row}
              >
                <label style={styles.rowLabel}>
                  <input
                    type="checkbox"
                    style={styles.checkbox}
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
                <OwnerFieldSelect
                  aria-label={`Owner field of ${object.label}`}
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
                </OwnerFieldSelect>
              </CardContent>
            );
          })}
        </Card>
      </Section>
      <Section>
        <H2Title title="Roles that see every row" />
        <Card rounded>
          {data.roles.map((role, index) => {
            const isAdmin = role.label === ADMIN_ROLE_LABEL;

            return (
              <CardContent
                key={role.id}
                divider={index < data.roles.length - 1}
                style={styles.row}
              >
                <label style={styles.rowLabel}>
                  <input
                    type="checkbox"
                    style={styles.checkbox}
                    checked={
                      isAdmin || draftConfig.seeAllRoleIds.includes(role.id)
                    }
                    disabled={isAdmin}
                    onChange={(event) =>
                      setDraftConfig(
                        setSeeAllRole(draftConfig, role.id, event.target.checked),
                      )
                    }
                  />
                  {role.label}
                </label>
                {isAdmin && <span style={styles.mutedText}>Always</span>}
              </CardContent>
            );
          })}
        </Card>
      </Section>
      <div>
        <Button
          type="button"
          variant="solid"
          color="accent"
          disabled={isSaving}
          onClick={handleSave}
        >
          {isSaving ? 'Saving…' : 'Save'}
        </Button>
      </div>
    </div>
  );
};

const RowAccessSettingsContent = () => {
  const state = useRowAccessSettings();
  const styles = getRowAccessSettingsStyles();

  if (state.kind === 'loading') {
    return <p style={styles.mutedText}>Loading row access…</p>;
  }

  if (state.kind === 'error') {
    return <p style={styles.mutedText}>{state.message}</p>;
  }

  return <RowAccessSettingsForm data={state.data} />;
};

export const RowAccessSettings = () => {
  const colorScheme = useColorScheme();

  return (
    <ThemeProvider colorScheme={colorScheme}>
      <RowAccessSettingsContent />
    </ThemeProvider>
  );
};
