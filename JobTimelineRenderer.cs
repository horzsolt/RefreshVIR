using RefreshVIR;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

public static class JobTimelineRenderer
{
    public static Bitmap CreateJobTimelineChart(
        List<JobExecution> jobs,
        int width,
        int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        DateTime now = DateTime.Now;

        DateTime timelineStart =
            DateTime.Today.AddDays(-1).AddHours(20);

        DateTime timelineEnd = now;

        TimeSpan totalSpan =
            timelineEnd - timelineStart;

        // -------------------------------------------------
        // Layout constants
        // -------------------------------------------------

        int leftMargin = 180;
        int rightMargin = 40;
        int topMargin = 30;
        int bottomMargin = 50;

        int laneHeight = 26;
        int barHeight = 14;

        // -------------------------------------------------
        // Distinct jobs / lanes
        // -------------------------------------------------

        List<string> distinctJobs =
            jobs
                .Select(j => j.JobName)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

        Dictionary<string, int> laneMap =
            new Dictionary<string, int>();

        for (int i = 0; i < distinctJobs.Count; i++)
        {
            laneMap[distinctJobs[i]] = i;
        }

        int laneCount =
            Math.Max(1, distinctJobs.Count);

        int paddingBottom = 30;
        int availableChartHeight =
            height - topMargin - bottomMargin - paddingBottom;

        laneHeight = Math.Max(16, availableChartHeight / laneCount);
        barHeight = Math.Max(8, laneHeight - 10);

        int chartHeight = laneCount * laneHeight;

        // -------------------------------------------------
        // Bitmap
        // -------------------------------------------------

        Bitmap bmp =
            new Bitmap(width, height);

        using Graphics g =
            Graphics.FromImage(bmp);

        g.SmoothingMode =
            SmoothingMode.AntiAlias;

        g.Clear(Color.White);

        int chartWidth =
            width - leftMargin - rightMargin;

        // -------------------------------------------------
        // Fonts (no title, only axis labels)
        // -------------------------------------------------

        using Font axisFont =
            new Font("Segoe UI", 8);

        using Font barFont =
            new Font("Segoe UI", 6);

        // -------------------------------------------------
        // Hour grid
        // -------------------------------------------------

        using Pen gridPen =
            new Pen(Color.LightGray);

        DateTime hour =
            new DateTime(
                timelineStart.Year,
                timelineStart.Month,
                timelineStart.Day,
                timelineStart.Hour,
                0,
                0);

        while (hour <= timelineEnd)
        {
            double pct =
                (hour - timelineStart).TotalSeconds /
                totalSpan.TotalSeconds;

            int x =
                leftMargin +
                (int)(pct * chartWidth);

            g.DrawLine(
                gridPen,
                x,
                topMargin,
                x,
                topMargin + chartHeight);

            string label =
                hour.ToString("HH:mm");

            SizeF size =
                g.MeasureString(label, axisFont);

            g.DrawString(
                label,
                axisFont,
                Brushes.Black,
                x - (size.Width / 2),
                topMargin + chartHeight + 5);

            hour = hour.AddHours(1);
        }

        // -------------------------------------------------
        // Job labels (left side)
        // -------------------------------------------------

        for (int i = 0; i < distinctJobs.Count; i++)
        {
            int y =
                topMargin +
                (i * laneHeight);

            g.DrawString(
                distinctJobs[i],
                axisFont,
                Brushes.Black,
                5,
                y + 2);

            g.DrawLine(
                Pens.Gainsboro,
                leftMargin,
                y + (barHeight / 2),
                leftMargin + chartWidth,
                y + (barHeight / 2));
        }

        // -------------------------------------------------
        // Status colors
        // -------------------------------------------------

        Color GetStatusColor(int status)
        {
            return status switch
            {
                0 => Color.Firebrick,
                1 => Color.ForestGreen,
                2 => Color.DarkOrange,
                3 => Color.Gray,
                4 => Color.DodgerBlue,
                _ => Color.SteelBlue
            };
        }

        // -------------------------------------------------
        // Bars
        // -------------------------------------------------

        foreach (JobExecution job in jobs)
        {
            if (job.FinishTime < timelineStart)
                continue;

            if (job.StartTime > timelineEnd)
                continue;

            DateTime visibleStart =
                job.StartTime < timelineStart
                    ? timelineStart
                    : job.StartTime;

            DateTime visibleFinish =
                job.FinishTime > timelineEnd
                    ? timelineEnd
                    : job.FinishTime;

            double startPct =
                (visibleStart - timelineStart).TotalSeconds /
                totalSpan.TotalSeconds;

            double finishPct =
                (visibleFinish - timelineStart).TotalSeconds /
                totalSpan.TotalSeconds;

            int x1 =
                leftMargin +
                (int)(startPct * chartWidth);

            int x2 =
                leftMargin +
                (int)(finishPct * chartWidth);

            int widthBar =
                Math.Max(3, x2 - x1);

            int lane =
                laneMap[job.JobName];

            int y =
                topMargin +
                (lane * laneHeight);

            Rectangle rect =
                new Rectangle(
                    x1,
                    y,
                    widthBar,
                    barHeight);

            using Brush b =
                new SolidBrush(GetStatusColor(job.RunStatus));

            g.FillRectangle(b, rect);

            g.DrawRectangle(Pens.Black, rect);

            // Minimal text only if space allows
            if (widthBar > 60)
            {
                g.DrawString(
                    $"{job.StartTime:HH:mm}",
                    barFont,
                    Brushes.White,
                    x1 + 2,
                    y + 1);
            }
        }

        // -------------------------------------------------
        // NOW marker
        // -------------------------------------------------

        double nowPct =
            (now - timelineStart).TotalSeconds /
            totalSpan.TotalSeconds;

        int nowX =
            leftMargin +
            (int)(nowPct * chartWidth);

        using Pen nowPen =
            new Pen(Color.Red, 2)
            {
                DashStyle = DashStyle.Dash
            };

        g.DrawLine(
            nowPen,
            nowX,
            topMargin,
            nowX,
            topMargin + chartHeight);

        return bmp;
    }
}