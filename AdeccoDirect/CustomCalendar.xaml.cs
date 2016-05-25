using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AdeccoDirecter
{
    /// <summary>
    /// Interaction logic for CustomCalendar.xaml
    /// </summary>
    public partial class CustomCalendar : UserControl
    {
        private Thickness _lastThickness;
        private DayButton _currentlyPressed;

        private Color primaryColor = new Color() { A = Convert.ToByte(255), R = Convert.ToByte(255), G = Convert.ToByte(255), B = Convert.ToByte(240) };
        private Color altColor = new Color() { A = Convert.ToByte(255), R = Convert.ToByte(255), G = Convert.ToByte(255), B = Convert.ToByte(208) };
        private Color disabledColor = new Color() { A = Convert.ToByte(255 / 2), R = Convert.ToByte(189), G = Convert.ToByte(189), B = Convert.ToByte(189) };

        private List<DayButton> dayButtons = new List<DayButton>();
        private List<Shift> shiftsInNextMonth = new List<Shift>();

        private DateTime _displayMonth;

        public CustomCalendar()
        {
            InitializeComponent();

            _displayMonth = DateTime.Now;
            GenerateCalendar(_displayMonth);
        }

        public void NextMonth()
        {
            _displayMonth = new DateTime(_displayMonth.Year, _displayMonth.Month, 1).AddMonths(1);
            if (_displayMonth.Month == DateTime.Now.Month && _displayMonth.Year == DateTime.Now.Year)
                _displayMonth = _displayMonth.AddDays(DateTime.Now.Day - 1);
            GenerateCalendar(_displayMonth);
        }

        public void PreviousMonth()
        {
            _displayMonth = new DateTime(_displayMonth.Year, _displayMonth.Month, 1).AddMonths(-1);
            if (_displayMonth.Month == DateTime.Now.Month && _displayMonth.Year == DateTime.Now.Year)
               _displayMonth =  _displayMonth.AddDays(DateTime.Now.Day - 1);
            GenerateCalendar(_displayMonth);
        }

        private void GenerateCalendar(DateTime now)
        {
            LayoutGrid.Children.Clear();
            var lastDayOfLastMonth = new DateTime(now.Year, now.Month, 1).AddDays(-1);
            var lastDayOfCurrentMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);

            var days = (int)lastDayOfLastMonth.DayOfWeek == 0 ? 7 : (int)lastDayOfLastMonth.DayOfWeek;
            var dateCounter = lastDayOfLastMonth.AddDays(-Math.Abs(1 - days));
            var lastDay = lastDayOfLastMonth.Day;

            int row = 1;
            int column = 0;

            bool markedCurrent = false;
            bool passedMonthEnd = false;

            dayButtons.Clear();

            for (int i = 0; i < (6 * 7); i++)
            {
                var dayButton = new DayButton();
                dayButton.BorderBrush = new SolidColorBrush(Colors.White);
                dayButton.BorderThickness = GetBorderThickness(row, column);

                var displayText = dateCounter.Day.ToString();
                if (lastDay != -1 || dateCounter.Day < now.Day && !passedMonthEnd)
                {
                    dayButton.Background = new SolidColorBrush(disabledColor);
                    dayButton.SetText(displayText);
                }
                else if (passedMonthEnd)
                {
                    dayButton.Background = new SolidColorBrush(disabledColor);
                    dayButton.SetText(displayText);
                }
                else if (dateCounter.Day == now.Day && !markedCurrent)
                {
                    dayButton.BorderBrush = new SolidColorBrush(Colors.Blue);
                    dayButton.Background = (GetBackground(row, column));
                    dayButton.SetText(displayText);
                    markedCurrent = true;
                }
                else
                {
                    dayButton.Background = GetBackground(row, column);
                    dayButton.SetText(displayText);
                }

                Grid.SetColumn(dayButton, column);
                Grid.SetRow(dayButton, row);
                dayButton.Year = dateCounter.Year;
                dayButton.Month = dateCounter.Month;
                dayButton.Day = dateCounter.Day;
                LayoutGrid.Children.Add(dayButton);
                dayButtons.Add(dayButton);
                dayButton.MouseUp += DayButton_MouseUp1;

                if (dateCounter.Day == lastDay)
                {
                    dateCounter = dateCounter.AddDays(1);
                    lastDay = -1;
                }
                else if (dateCounter == lastDayOfCurrentMonth)
                {
                    passedMonthEnd = true;
                    dateCounter = dateCounter.AddDays(1);
                }
                else
                {
                    dateCounter = dateCounter.AddDays(1);
                }
                column++;
                if ((i + 1) % 7 == 0 && i != 0)
                {
                    column = 0;
                    row++;
                }
            }
        }

        private void DayButton_MouseUp1(object sender, MouseButtonEventArgs e)
        {
        }

        private Thickness GetBorderThickness(int row, int column)
        {
            Thickness thickness;
            if (column == 0 && row == 2)
                thickness = new Thickness(2, 2, 1, 1);
            else if (column == 6 && row == 2)
                thickness = new Thickness(1, 2, 2, 1);
            else if (row == 2)
                thickness = new Thickness(1, 2, 1, 1);
            else if (column == 0 && row == 7)
                thickness = new Thickness(2, 1, 1, 2);
            else if (column == 6 && row == 7)
                thickness = new Thickness(1, 1, 2, 2);
            else if (row == 7)
                thickness = new Thickness(1, 1, 1, 2);
            else if (column == 0)
                thickness = new Thickness(2, 1, 1, 1);
            else if (column == 6)
                thickness = new Thickness(1, 1, 2, 1);
            else
                thickness = new Thickness(1);

            return thickness;
        }

        private SolidColorBrush GetBackground(int row, int column)
        {
            SolidColorBrush background;
            if (row % 2 == 0 && column % 2 == 0)
            {
                background = new SolidColorBrush(primaryColor);
            }
            else if (row % 2 != 0 && column % 2 != 0)
            {
                background = new SolidColorBrush(primaryColor);
            }
            else
            {
                background = new SolidColorBrush(altColor);
            }

            return background;
        }

        public void SetShifts(List<Shift> shifts)
        {
            foreach (var shift in shifts)
            {
                var dayButton = dayButtons.FirstOrDefault(x => x.Day == shift.StartTime.Day && x.Month == shift.StartTime.Month && x.Year == shift.StartTime.Year);
                if (dayButton == null)
                    shiftsInNextMonth.Add(shift);
                else
                {
                    dayButton.Shift = shift;
                    this.Dispatcher.Invoke(() =>
                    {
                        dayButton.SetText(string.Format("{0} - {1}\r\n{2}", shift.StartTime.ToShortTimeString(), shift.EndTime.ToShortTimeString(), shift.ShiftName));
                    });
                }
            }
        }
    }

    public partial class DayButton : UserControl
    {
        public int Day { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public Shift Shift { get; set; }

        private StackPanel layout = new StackPanel() { Orientation = Orientation.Vertical };

        protected override void OnInitialized(EventArgs e)
        {
            AddChild(layout);
            base.OnInitialized(e);
        }

        public void SetText(string text)
        {
            layout.Children.Add(new TextBlock() { Text = text });
        }
    }
}
