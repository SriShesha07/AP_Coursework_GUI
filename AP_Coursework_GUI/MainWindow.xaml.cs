using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace AP_Coursework_GUI
{
    
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Evaluate_Click(object sender, RoutedEventArgs e)
        {
            string input = InputTextBox.Text.Trim();
            if(string.IsNullOrEmpty(input))
            {
                ErrorBox.Text = "Please enter an expression.";
                return;
            }
            try
            {
                string result = ExprEvaluator.EvaluateExpression(input);
                if (result.StartsWith("Lexer") || result.StartsWith("Parser") ||
                   result.StartsWith("Runtime") || result.StartsWith("Error"))
                {
                    ErrorBox.Text = result;
                    ResultTextBox.Clear();
                }
                else
                {
                    ResultTextBox.Text = result;
                    ErrorBox.Clear();
                }
            }
            catch(Exception ex)
            {
                ResultTextBox.Text = $"Error: {ex.Message}";
            }

        }

        private void Plot_Click(object sender, RoutedEventArgs e)
        {
            ErrorBox.Clear();
            ResultTextBox.Clear();

            if (!double.TryParse(XMinBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double xmin) ||
                !double.TryParse(XMaxBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double xmax) ||
                !double.TryParse(StepBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double step))
            {
                ErrorBox.Text = "Invalid range values. Use numbers for Xmin, Xmax, and Step.";
                return;
            }
            if (step <= 0 || xmax <= xmin)
            {
                ErrorBox.Text = "Ensure: Step > 0 and Xmax > Xmin.";
                return;
            }

            string input = InputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                ErrorBox.Text = "Enter definitions and a final expression in x to plot.";
                return;
            }

            var lines = input.Split(new[] {'\n',';'}, StringSplitOptions.RemoveEmptyEntries)
                             .Select(s => s.Trim())
                             .Where(s => s.Length>0)
                             .ToArray();
            if (lines.Length == 0)
            {
                ErrorBox.Text = "Nothing to plot.";
                return;
            }
            string expr = lines[lines.Length-1];
            var setup = lines.Take(lines.Length-1);

            ExprEvaluator.ResetState();
            foreach (var stmt in setup)
            {
                string res = ExprEvaluator.EvaluateExpression(stmt);
                if (res.StartsWith("Lexer") || res.StartsWith("Parser") || res.StartsWith("Runtime") || res.StartsWith("Error"))
                {
                    ErrorBox.Text = $"Setup error: {res}";
                    return;
                }
            }

            var points = new List<Point>();
            double x = xmin;
            int guard = 0;
            while (x <= xmax + 1e-12 && guard < 100000)
            {
                string yStr = ExprEvaluator.EvaluateExprForX(expr, x);
                if (yStr.StartsWith("Lexer") || yStr.StartsWith("Parser") || yStr.StartsWith("Runtime") || yStr.StartsWith("Error"))
                {
                    ErrorBox.Text = $"At x={x.ToString("0.###", CultureInfo.InvariantCulture)}: {yStr}";
                    return;
                }
                if (double.TryParse(yStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    points.Add(new Point(x, y));
                }
                x += step;
                guard++;
            }
            if (points.Count < 2)
            {
                ErrorBox.Text = "Not enough points to plot.";
                return;
            }

            DrawPlot(points, xmin, xmax);
        }

        private void DrawPlot(List<Point> data, double xmin, double xmax)
        {
            PlotCanvas.Children.Clear();
            double width = Math.Max(PlotCanvas.ActualWidth, 10);
            double height = Math.Max(PlotCanvas.ActualHeight, 10);
            if (width < 50) width = 800; if (height < 50) height = 400;
            double pad = 40;

            double ymin = data.Min(p => p.Y);
            double ymax = data.Max(p => p.Y);
            if (Math.Abs(ymax - ymin) < 1e-9) { ymax = ymin + 1; }

            Func<double,double> xToPx = xv => pad + (xv - xmin) / (xmax - xmin) * (width - 2*pad);
            Func<double,double> yToPy = yv => height - pad - (yv - ymin) / (ymax - ymin) * (height - 2*pad);

            if (xmin <= 0 && 0 <= xmax)
            {
                var x0 = xToPx(0);
                var vline = new Line { X1 = x0, X2 = x0, Y1 = pad, Y2 = height - pad, Stroke = Brushes.LightGray, StrokeThickness = 1 };
                PlotCanvas.Children.Add(vline);
            }
            if (ymin <= 0 && 0 <= ymax)
            {
                var y0 = yToPy(0);
                var hline = new Line { X1 = pad, X2 = width - pad, Y1 = y0, Y2 = y0, Stroke = Brushes.LightGray, StrokeThickness = 1 };
                PlotCanvas.Children.Add(hline);
            }

            double NiceStep(double range, int targetTicks = 8)
            {
                if (range <= 0 || double.IsNaN(range) || double.IsInfinity(range)) return 1.0;
                double raw = range / Math.Max(1, targetTicks);
                double p = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(raw, 1e-12))));
                double m = raw / p;
                if (m < 1.5) return 1 * p;
                if (m < 3) return 2 * p;
                if (m < 7) return 5 * p;
                return 10 * p;
            }
            string NumLabel(double v)
            {
                if (Math.Abs(v - Math.Round(v)) < 1e-9) return Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
                return v.ToString("0.###", CultureInfo.InvariantCulture);
            }

            {
                double xrange = xmax - xmin;
                double step = NiceStep(xrange);
                double start = Math.Ceiling(xmin / step) * step;
                double yBase = height - pad;
                double tickLen = 6;
                int guard = 0;
                for (double xv = start; xv <= xmax + 1e-12 && guard < 10000; xv += step, guard++)
                {
                    double px = xToPx(xv);
                    var t = new Line { X1 = px, X2 = px, Y1 = yBase, Y2 = yBase - tickLen, Stroke = Brushes.Gray, StrokeThickness = 1 };
                    PlotCanvas.Children.Add(t);

                    var tb = new TextBlock { Text = NumLabel(xv), FontSize = 11, Foreground = Brushes.Gray };
                    tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    PlotCanvas.Children.Add(tb);
                    Canvas.SetLeft(tb, px - tb.DesiredSize.Width / 2);
                    Canvas.SetTop(tb, yBase + 2);
                }
            }

            {
                double yrange = ymax - ymin;
                double step = NiceStep(yrange);
                double start = Math.Ceiling(ymin / step) * step;
                double xBase = pad;
                double tickLen = 6;
                int guard = 0;
                for (double yv = start; yv <= ymax + 1e-12 && guard < 10000; yv += step, guard++)
                {
                    double py = yToPy(yv);
                    var t = new Line { X1 = xBase, X2 = xBase + tickLen, Y1 = py, Y2 = py, Stroke = Brushes.Gray, StrokeThickness = 1 };
                    PlotCanvas.Children.Add(t);

                    var tb = new TextBlock { Text = NumLabel(yv), FontSize = 11, Foreground = Brushes.Gray };
                    tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    PlotCanvas.Children.Add(tb);
                    Canvas.SetLeft(tb, xBase - tb.DesiredSize.Width - 4);
                    Canvas.SetTop(tb, py - tb.DesiredSize.Height / 2);
                }
            }

            var poly = new Polyline { Stroke = Brushes.SteelBlue, StrokeThickness = 2 };
            foreach (var p in data)
            {
                poly.Points.Add(new System.Windows.Point(xToPx(p.X), yToPy(p.Y)));
            }
            PlotCanvas.Children.Add(poly);
        }

        private void HelpMenu_Click(object sender, RoutedEventArgs e)
        {
            string helpText =
                    "Valid tokens and syntax:\n" +
                    "  Integers (e.g., 10)\n" +
                    "  Floats (e.g., 23.45)\n" +
                    "  Identifiers (variables/functions): start with a letter, then letters/digits/_\n" +
                    "  Operators: +  -  *  /  %  ^\n" +
                    "  Parentheses: ( )\n\n" +
                    "Statements (separate by newline or ';'):\n" 
                ;
                
            MessageBox.Show(helpText, "Help - Syntax and Tokens",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

    }
}