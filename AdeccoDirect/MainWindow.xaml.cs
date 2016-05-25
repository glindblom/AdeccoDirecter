using HtmlAgilityPack;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Xml;

namespace AdeccoDirecter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Shifts
        private const string shiftMFN = "0530-1245";
        private const string shiftFDefault = "0630-1500";
        private const string shiftFWeekend = "0730-1600";
        private const string shiftD = "1000-1930";
        private const string shiftEDS = "1300-2100";
        private const string shiftEDefault = "1400-2230";
        private const string shiftEWeekend = "1500-2230";
        private const string shiftRastShort = "1600-2030";
        private const string shiftExtra = "1600-2000";
        private const string shiftRastLong = "1630-2130";
        private const string shiftNight = "2130-0730";

        private const string CALENDAR_FORMAT =
            "BEGIN:VCALENDAR\r\n" +
            "VERSION:2.0\r\n" +
            "PRODID:-//hacksw/handcal//NONSGML v1.0//EN\r\n" +
            "{0}\r\n" +
            "END:VCALENDAR";

        private const string EVENT_FORMAT =
            "BEGIN:VEVENT\r\n" +
            "ORGANIZER;CN={0}:MAILTO:jennifer.olin@adecco.se\r\n" +
            "DTSTART:{1}\r\n" +
            "DTEND:{2}\r\n" +
            "SUMMARY:{3}\r\n" +
            "END:VEVENT\r\n";

        private const string COMPANY_NAME_ID = "cphMain_ucBookedShiftList_gvShifts_lblCompanyName";
        private const string DATE_ID = "cphMain_ucBookedShiftList_gvShifts_lblDate";
        private const string NOTE_ID = "cphMain_ucBookedShiftList_gvShifts_lblNote";

        private const string PREVIOUS_COMPANY_ID = "cphMain_ucOldShifts_gvShifts_lblCompanyName";
        private const string PREVIOUS_DATE_ID = "cphMain_ucOldShifts_gvShifts_lblDate";
        private const string PREVIOUS_NOTE_ID = "cphMain_ucOldShifts_gvShifts_lblNote";

        private Uri _loginUri;
        private Uri _loggedInUri;
        private Uri _shiftsUri;

        private HtmlDocument _shiftsDocument;
        private List<Shift> bookedShifts = new List<Shift>();
        private List<Shift> previousShifts = new List<Shift>();

        private DateTime _displayMonth = DateTime.Now;

        public MainWindow()
        {
            InitializeComponent();

            _loginUri = new Uri("https://www.direct.adecco.se");
            _loggedInUri = new Uri("https://www.direct.adecco.se/Users/Consultant/Shifts.aspx");
            _shiftsUri = new Uri("https://www.direct.adecco.se/Users/Consultant/MyBookedShifts.aspx");

            Start();
        }

        public void Start()
        {
            _browser.Navigated += OnBrowserNavigated;

            _browser.Navigate(_loginUri);
        }

        private void OnBrowserNavigated(object sender, NavigationEventArgs e)
        {
            SetSilent(_browser, true);
            if (e.Uri == _loggedInUri)
            {
                _browser.Visibility = Visibility.Collapsed;
                _loading.Visibility = Visibility.Visible;

                var task = Task.Factory.StartNew(() => { ParseShifts(bookedShifts); Calendar.SetShifts(bookedShifts.Concat(previousShifts).ToList()); }).ContinueWith(t => ShiftVisiblity());
            }
        }

        private void ShiftVisiblity()
        {
            this.Dispatcher.Invoke(() =>
            {
                _loading.Visibility = Visibility.Collapsed;
                _loggedIn.Visibility = Visibility.Visible;
            });
        }

        private void ParseShifts(List<Shift> shiftsList)
        {
            bookedShifts.Clear();
            var cookieString = Application.GetCookie(_loginUri).Split('=');

            var request = (HttpWebRequest)WebRequest.Create(_shiftsUri);
            request.KeepAlive = true;
            request.CookieContainer = new CookieContainer();
            request.CookieContainer.Add(new Cookie(cookieString[0], cookieString[1], "/", "www.direct.adecco.se"));

            var response = request.GetResponse();

            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                _shiftsDocument = new HtmlDocument();
                var responseString = reader.ReadToEnd();
                _shiftsDocument.LoadHtml(responseString);
            }

            var table = _shiftsDocument.GetElementbyId("cphMain_ucBookedShiftList_gvShifts");

            var dataNodes = table.SelectNodes("tr/td");
            Shift bookedShift = new Shift();

            foreach (var node in dataNodes)
            {
                var child = node.SelectSingleNode("span");
                if (child == null) continue;

                if (child.Id.Contains(COMPANY_NAME_ID))
                {
                    bookedShift.Company = child.InnerText;
                }
                else if (child.Id.Contains(DATE_ID))
                {
                    var dateTest = child.InnerText.Trim().Split(',');
                    var date = DateTime.Parse(dateTest[0]);
                    var start = dateTest[1].Split('-')[0];
                    var end = dateTest[1].Split('-')[1];
                    var startTime = new DateTime(date.Year, date.Month, date.Day, Int32.Parse(start.Split(':')[0]), Int32.Parse(start.Split(':')[1]), 0);
                    var endTime = new DateTime(date.Year, date.Month, date.Day, Int32.Parse(end.Split(':')[0]), Int32.Parse(end.Split(':')[1]), 0);

                    if (endTime.Hour < startTime.Hour)
                    {
                        endTime = endTime.AddDays(1);
                    }

                    bookedShift.StartTime = startTime;
                    bookedShift.EndTime = endTime;
                }
                else if (child.Id.Contains(NOTE_ID))
                {
                    bookedShift.Comment = child.InnerText;
                    shiftsList.Add(new Shift() { Company = bookedShift.Company, StartTime = bookedShift.StartTime, EndTime = bookedShift.EndTime, Comment = bookedShift.Comment, ShiftName = GetShiftName(bookedShift) });
                    bookedShift = new Shift();
                }
            }

            table = _shiftsDocument.GetElementbyId("cphMain_ucOldShifts_gvShifts");
            dataNodes = table.SelectNodes("tr/td");

            Shift previousShift = new Shift();

            foreach (var node in dataNodes)
            {
                var child = node.SelectSingleNode("span");
                if (child == null) continue;

                if (child.Id.Contains(PREVIOUS_COMPANY_ID))
                    previousShift.Company = child.InnerText;
                else if (child.Id.Contains(PREVIOUS_DATE_ID))
                {
                    var dateTest = child.InnerText.Trim().Split(',');
                    var date = DateTime.Parse(dateTest[0]);
                    var start = dateTest[1].Split('-')[0];
                    var end = dateTest[1].Split('-')[1];
                    var startTime = new DateTime(date.Year, date.Month, date.Day, Int32.Parse(start.Split(':')[0]), Int32.Parse(start.Split(':')[1]), 0);
                    var endTime = new DateTime(date.Year, date.Month, date.Day, Int32.Parse(end.Split(':')[0]), Int32.Parse(end.Split(':')[1]), 0);

                    if (endTime.Hour < startTime.Hour)
                    {
                        endTime = endTime.AddDays(1);
                    }

                    previousShift.StartTime = startTime;
                    previousShift.EndTime = endTime;
                }
                else if (child.Id.Contains(PREVIOUS_NOTE_ID))
                {
                    previousShift.Comment = child.InnerText;
                    previousShifts.Add(new Shift() { Company = bookedShift.Company, StartTime = bookedShift.StartTime, EndTime = bookedShift.EndTime, Comment = bookedShift.Comment, ShiftName = GetShiftName(bookedShift) });
                    previousShift = new Shift();
                }
            }
        }

        public static void SetSilent(WebBrowser browser, bool silent)
        {
            if (browser == null)
                throw new ArgumentNullException("browser");

            // get an IWebBrowser2 from the document
            IOleServiceProvider sp = browser.Document as IOleServiceProvider;
            if (sp != null)
            {
                Guid IID_IWebBrowserApp = new Guid("0002DF05-0000-0000-C000-000000000046");
                Guid IID_IWebBrowser2 = new Guid("D30C1661-CDAF-11d0-8A3E-00C04FC9E26E");

                object webBrowser;
                sp.QueryService(ref IID_IWebBrowserApp, ref IID_IWebBrowser2, out webBrowser);
                if (webBrowser != null)
                {
                    webBrowser.GetType().InvokeMember("Silent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.PutDispProperty, null, webBrowser, new object[] { silent });
                }
            }
        }

        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IOleServiceProvider
        {
            [PreserveSig]
            int QueryService([In] ref Guid guidService, [In] ref Guid riid, [MarshalAs(UnmanagedType.IDispatch)] out object ppvObject);
        }

        private void iCalGen_Click(object sender, RoutedEventArgs e)
        {
            string savePath = GetSavePath();
            if (savePath == null) return;
            if (SaveCalendarFile(savePath))
                MessageBox.Show("Klart! Kalenderfilen har skapats.", "Success", MessageBoxButton.OK);
            else
                MessageBox.Show("Nej! Kalenderfilen kunde inte skapas, och programmeraren är alldeles för lat för att ta reda på varför.", "Error", MessageBoxButton.OK);
        }

        private string GetSavePath()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.DefaultExt = "ical";
            sfd.FileName = "Kalender";
            sfd.AddExtension = true;
            sfd.OverwritePrompt = true;
            sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            if (sfd.ShowDialog().Value)
            {
                return sfd.FileName;
            }
            else return null;
        }

        private bool SaveCalendarFile(string path)
        {
            string events = "";
            foreach (var shift in bookedShifts)
            {
                string company = shift.Company;
                string start = shift.StartTime.ToString("yyyyMMdd'T'HHmmss");
                string end = shift.EndTime.ToString("yyyyMMdd'T'HHmmss");
                string summary = shift.ShiftName;

                events += String.Format(EVENT_FORMAT, company, start, end, summary);
            }

            string calendarString = String.Format(CALENDAR_FORMAT, events);
            try
            {
                File.WriteAllText(@path, calendarString);
                return true;
            }
            catch { return false; }
        }

        private string GetShiftName(Shift shift)
        {
            string summary;
            string shiftComparer = String.Format("{0}-{1}", shift.StartTime.ToString("HHmm"), shift.EndTime.ToString("HHmm"));

            switch (shiftComparer)
            {
                case shiftMFN:
                    summary = "MFN1";
                    break;
                case shiftFDefault:
                case shiftFWeekend:
                    summary = "F-pass";
                    break;
                case shiftD:
                    summary = "D-pass";
                    break;
                case shiftEDS:
                    summary = "EDS-pass";
                    break;
                case shiftEDefault:
                case shiftEWeekend:
                    summary = "E-pass";
                    break;
                case shiftRastShort:
                case shiftRastLong:
                    summary = "Rastavlösare";
                    break;
                case shiftExtra:
                    summary = "Extra Södra";
                    break;
                case shiftNight:
                    summary = "N-pass";
                    break;
                default:
                    summary = "Extra";
                    break;
            }

            return summary;
        }

        private void MonthForward_Click(object sender, RoutedEventArgs e)
        {
            Calendar.NextMonth();
            _loggedIn.Visibility = Visibility.Collapsed;
            _loading.Visibility = Visibility.Visible;

            StartTask();
        }

        private void MonthBackwards_Click(object sender, RoutedEventArgs e)
        {
            Calendar.PreviousMonth();
            _loggedIn.Visibility = Visibility.Collapsed;
            _loading.Visibility = Visibility.Visible;

            StartTask();
        }

        private void StartTask()
        {
            var task = Task.Factory.StartNew(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    MonthForward.IsEnabled = false;
                    MonthBackwards.IsEnabled = false;
                });
            }).ContinueWith(t =>
            {
                ParseShifts(bookedShifts);
                Calendar.SetShifts(bookedShifts);
            }).ContinueWith(t =>
            {
                ShiftVisiblity();
                Dispatcher.Invoke(() =>
                {
                    MonthForward.IsEnabled = true;
                    MonthBackwards.IsEnabled = true;
                });
            });
        }
    }

    public struct Shift
    {
        public string Company { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string ShiftName { get; set; }
        public string Comment { get; set; }
    }
}
