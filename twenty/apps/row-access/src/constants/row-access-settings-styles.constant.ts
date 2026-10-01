import { type CSSProperties } from 'react';
import { themeCssVariables } from 'twenty-ui/theme-constants';

// A function, not constants: the manifest build loads this module without the
// theme tokens, so they can only be read at render time.
export const getRowAccessSettingsStyles = () => ({
  page: {
    boxSizing: 'border-box',
    display: 'flex',
    flexDirection: 'column',
    fontFamily: themeCssVariables.font.family,
    gap: themeCssVariables.spacing[8],
    width: '100%',
  } satisfies CSSProperties,
  row: {
    alignItems: 'center',
    display: 'flex',
    gap: themeCssVariables.spacing[4],
    justifyContent: 'space-between',
    boxSizing: 'border-box',
    minHeight: themeCssVariables.spacing[12],
    padding: `${themeCssVariables.spacing[2]} ${themeCssVariables.spacing[3]}`,
  } satisfies CSSProperties,
  rowLabel: {
    alignItems: 'center',
    color: themeCssVariables.font.color.primary,
    display: 'flex',
    fontSize: themeCssVariables.font.size.md,
    gap: themeCssVariables.spacing[2],
  } satisfies CSSProperties,
  checkbox: {
    accentColor: themeCssVariables.color.blue,
    cursor: 'pointer',
    height: themeCssVariables.spacing[4],
    margin: 0,
    width: themeCssVariables.spacing[4],
  } satisfies CSSProperties,
  mutedText: {
    color: themeCssVariables.font.color.tertiary,
    fontSize: themeCssVariables.font.size.md,
    margin: 0,
  } satisfies CSSProperties,
  ownerFieldSelect: {
    appearance: 'auto',
    backgroundColor: themeCssVariables.background.transparent.lighter,
    border: `1px solid ${themeCssVariables.border.color.medium}`,
    borderRadius: themeCssVariables.border.radius.md,
    boxSizing: 'border-box',
    fontFamily: themeCssVariables.font.family,
    fontSize: themeCssVariables.font.size.md,
    fontWeight: themeCssVariables.font.weight.regular,
    height: '32px',
    minWidth: '200px',
    outline: 'none',
    paddingInline: themeCssVariables.spacing[2],
  } satisfies CSSProperties,
});
