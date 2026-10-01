import { type ComponentProps, useState } from 'react';
import { themeCssVariables } from 'twenty-ui/theme-constants';

import { getRowAccessSettingsStyles } from 'src/constants/row-access-settings-styles.constant';

export const OwnerFieldSelect = ({
  children,
  ...props
}: Omit<ComponentProps<'select'>, 'style' | 'onFocus' | 'onBlur'>) => {
  const styles = getRowAccessSettingsStyles();
  const [isFocused, setIsFocused] = useState(false);

  return (
    <select
      {...props}
      style={{
        ...styles.ownerFieldSelect,
        borderColor: isFocused
          ? themeCssVariables.color.blue
          : themeCssVariables.border.color.medium,
        color: props.disabled
          ? themeCssVariables.font.color.tertiary
          : themeCssVariables.font.color.primary,
        cursor: props.disabled ? 'not-allowed' : 'pointer',
      }}
      onFocus={() => setIsFocused(true)}
      onBlur={() => setIsFocused(false)}
    >
      {children}
    </select>
  );
};
