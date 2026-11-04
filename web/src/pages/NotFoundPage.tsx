import { Button } from '@fluentui/react-components';
import { useNavigate } from 'react-router-dom';
import { EmptyState } from '../components/StateViews';

export function NotFoundPage() {
  const navigate = useNavigate();
  return (
    <EmptyState
      title="Page not found"
      description="The address you opened does not match a page in EDM 2.0."
      action={
        <Button appearance="primary" onClick={() => navigate('/workspace/overview')}>
          Go to Overview
        </Button>
      }
    />
  );
}
