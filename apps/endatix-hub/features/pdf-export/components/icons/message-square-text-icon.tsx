import { Path, Svg } from '@react-pdf/renderer';
import { PDF_STYLES } from '../pdf-styles';

const MessageSquareTextIcon = () => (
  <Svg style={PDF_STYLES.icon}>
      <Path
        d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"
        stroke="currentColor"
        strokeWidth={2}
        stroke-linecap="round"
        stroke-linejoin="round"
      />
      <Path
        d="M13 8H7"
        stroke="currentColor"
        strokeWidth={2}
        stroke-linecap="round"
        stroke-linejoin="round"
      />
      <Path
        d="M17 12H7"
        stroke="currentColor"
        strokeWidth={2}
        stroke-linecap="round"
        stroke-linejoin="round"
      />
    </Svg>
);

export default MessageSquareTextIcon;
