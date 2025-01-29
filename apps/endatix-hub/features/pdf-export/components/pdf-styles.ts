import { StyleSheet } from '@react-pdf/renderer';

export const PDF_STYLES = StyleSheet.create({
  image: {
    objectFit: 'cover',
    width: '100%',
    height: 'auto',
    wrap: 'false',
  },
  icon: {
    width: 24,
    height: 24,
  },
  flexRow: {
    display: 'flex',
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
    alignItems: 'center',
  },
  flexColumn: {
    display: 'flex',
    flexDirection: 'column',
    flexWrap: 'wrap',
  },
  justifyBetween: {
    justifyContent: 'space-between'
  },
  smallText: {
    fontSize: 10,
  },
  mutedText: {
    color: 'gray',
  },
  mutedBorder: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 4,
    padding: 8,
  },
  marginBottom: {
    marginBottom: 8,
  },
});
