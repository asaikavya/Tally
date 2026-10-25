import { themeQuartz } from "ag-grid-community"

// Tally's look, set once. Changing a color here restyles the whole grid.
export const tallyGridTheme = themeQuartz.withParams({
  fontFamily: "inherit",
  fontSize: 14,
  accentColor: "#1F5F8B",
  foregroundColor: "#1B1B18",
  borderColor: "#E4E2DC",
  headerBackgroundColor: "#FAFAF8",
  headerFontWeight: 700,
  rowHoverColor: "#F6F5F1",
  selectedRowBackgroundColor: "#EEF3F7",
  wrapperBorderRadius: 12,
})