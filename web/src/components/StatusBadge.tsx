import { Badge } from '@fluentui/react-components';
import { statusDefinition, type StatusKey } from '../status/statuses';

/**
 * Status indicator. Shows the label as text, so the status is never conveyed by colour alone.
 */
export function StatusBadge({ status }: { readonly status: StatusKey }) {
  const def = statusDefinition(status);
  return (
    <Badge appearance="filled" color={def.color} shape="rounded" size="medium">
      {def.label}
    </Badge>
  );
}
