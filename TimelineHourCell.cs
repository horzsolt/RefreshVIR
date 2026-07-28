using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace RefreshVIR
{
    internal sealed class TimelineHourColumn : DataGridViewColumn
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal DateTime SlotStart { get; set; }

        public TimelineHourColumn()
            : base(new TimelineHourCell())
        {
        }

        public override object Clone()
        {
            TimelineHourColumn clone = (TimelineHourColumn)base.Clone();
            clone.SlotStart = SlotStart;
            return clone;
        }
    }

    internal sealed class TimelineHourCell : DataGridViewCell
    {
        private const int BarPadding = 2;
        private const int MinBarWidthPixels = 3;

        internal List<TimelineCellExecutionSegment> Segments { get; set; } = new List<TimelineCellExecutionSegment>();

        public TimelineHourCell()
        {
            Style.BackColor = Color.White;
            Style.SelectionBackColor = Color.White;
        }

        public override Type ValueType => typeof(string);

        public override Type FormattedValueType => typeof(string);

        public override object DefaultNewRowValue => string.Empty;

        public override object Clone()
        {
            TimelineHourCell clone = (TimelineHourCell)base.Clone();
            clone.Segments = Segments
                .Select(segment => new TimelineCellExecutionSegment
                {
                    VisibleStart = segment.VisibleStart,
                    VisibleFinish = segment.VisibleFinish,
                    RunStatus = segment.RunStatus
                })
                .ToList();
            return clone;
        }

        protected override object GetFormattedValue(
            object? value,
            int rowIndex,
            ref DataGridViewCellStyle cellStyle,
            TypeConverter? valueTypeConverter,
            TypeConverter? formattedValueTypeConverter,
            DataGridViewDataErrorContexts context)
        {
            return string.Empty;
        }

        protected override void Paint(
            Graphics graphics,
            Rectangle clipBounds,
            Rectangle cellBounds,
            int rowIndex,
            DataGridViewElementStates cellState,
            object? value,
            object? formattedValue,
            string? errorText,
            DataGridViewCellStyle cellStyle,
            DataGridViewAdvancedBorderStyle advancedBorderStyle,
            DataGridViewPaintParts paintParts)
        {
            if (DataGridView == null)
                return;

            graphics.SmoothingMode = SmoothingMode.None;
            PaintCellBackground(graphics, cellBounds, cellStyle, cellState);

            if (OwningColumn is TimelineHourColumn hourColumn && Segments.Count > 0)
            {
                DateTime slotStart = hourColumn.SlotStart;
                DateTime slotEnd = slotStart.AddHours(1);
                PaintExecutionBars(graphics, cellBounds, slotStart, slotEnd, cellState);
            }

            PaintBorder(graphics, clipBounds, cellBounds, cellStyle, advancedBorderStyle);
        }

        private void PaintCellBackground(
            Graphics graphics,
            Rectangle cellBounds,
            DataGridViewCellStyle cellStyle,
            DataGridViewElementStates cellState)
        {
            Color background = (cellState & DataGridViewElementStates.Selected) != 0
                ? Color.FromArgb(245, 248, 255)
                : cellStyle.BackColor;

            using SolidBrush brush = new SolidBrush(background);
            graphics.FillRectangle(brush, cellBounds);
        }

        private void PaintExecutionBars(
            Graphics graphics,
            Rectangle cellBounds,
            DateTime slotStart,
            DateTime slotEnd,
            DataGridViewElementStates cellState)
        {
            Rectangle barArea = Rectangle.Inflate(cellBounds, -BarPadding, -BarPadding);
            if (barArea.Width <= 0 || barArea.Height <= 0)
                return;

            bool selected = (cellState & DataGridViewElementStates.Selected) != 0;

            foreach (TimelineCellExecutionSegment segment in Segments.OrderBy(s => s.VisibleStart))
            {
                (double startFraction, double widthFraction) = TimelineCellSegmentCalculator.GetBarFractions(
                    slotStart,
                    slotEnd,
                    segment.VisibleStart,
                    segment.VisibleFinish);

                if (widthFraction <= 0)
                    continue;

                int x = barArea.Left + (int)Math.Round(startFraction * barArea.Width);
                int width = Math.Max(MinBarWidthPixels, (int)Math.Round(widthFraction * barArea.Width));
                width = Math.Min(width, barArea.Right - x);

                if (width <= 0)
                    continue;

                Rectangle barBounds = new Rectangle(x, barArea.Top, width, barArea.Height);
                Color barColor = GetStatusColor(segment.RunStatus);

                if (selected)
                    barColor = ControlPaint.Light(barColor, 0.15f);

                using SolidBrush brush = new SolidBrush(barColor);
                graphics.FillRectangle(brush, barBounds);

                using Pen borderPen = new Pen(ControlPaint.Dark(barColor, 0.1f));
                graphics.DrawRectangle(borderPen, barBounds);
            }
        }

        private static Color GetStatusColor(int status) => status switch
        {
            0 => Color.Firebrick,
            1 => Color.ForestGreen,
            2 => Color.DarkOrange,
            3 => Color.Gray,
            4 => Color.DodgerBlue,
            _ => Color.SteelBlue
        };
    }
}
