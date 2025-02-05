import { Badge } from '@/components/ui/badge';

const getStatusLabel = (code: string) => {
  switch (code) {
    case 'new':
      return 'New';
    case 'seen':
      return 'Seen';
    case 'declined':
      return 'Declined';
    case 'approved':
      return 'Approved';
    default:
      return 'Unknown';
  }
};

interface CellSeenStatusProps {
  code: string;
}

export function CellSeenStatus({ code }: CellSeenStatusProps) {
  return (
    <Badge variant={code === 'new' ? 'default' : 'secondary'}>
      {getStatusLabel(code)}
    </Badge>
  );
}
