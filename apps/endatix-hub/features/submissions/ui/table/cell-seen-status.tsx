import { Badge } from '@/components/ui/badge';

const getSeenLabel = (code: string) => {
  switch (code) {
    case 'new':
      return 'New';
    case 'seen':
      return 'Seen';
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
      {getSeenLabel(code)}
    </Badge>
  );
}
